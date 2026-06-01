using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;
using Waao.Services.Messaging;

namespace Waao.Services.Services;

public sealed class MessageService(
	WaaoDbContext Db,
	INotificationService NotificationService,
	IPushNotificationService Push,
	IPresenceTracker Presence,
	ILogger<MessageService> Logger,
	IMessageTextProtector Protector,
	IR2StorageService Storage) : IMessageService
{
	/// <summary>Validity window for presigned (private) attachment URLs. Re-signed on every read,
	/// so this only needs to outlast a single open chat session.</summary>
	private static readonly TimeSpan AttachmentUrlTtl = TimeSpan.FromHours(12);

	/// <summary>Resolves the URL the client should use for an attachment: a fresh presigned URL for
	/// private objects (StorageKey set), or the stored public URL for legacy ones.</summary>
	private string ResolveAttachmentUrl(MessageAttachment a)
		=> a.StorageKey is { Length: > 0 } key ? Storage.GetPresignedUrl(key, AttachmentUrlTtl) : a.Url;

	// =====================================================================
	// GET MESSAGES (with before-cursor pagination)
	// =====================================================================

	public async Task<MessagePageDto> GetMessagesAsync(
		Guid channelId,
		Guid callerId,
		Guid? before,
		int limit,
		CancellationToken ct = default)
	{
		// Membership check
		var isMember = await Db.ChannelMembers
			.AnyAsync(m => m.ChannelId == channelId && m.CollaboratorId == callerId, ct);

		if (!isMember)
			throw new UnauthorizedAccessException($"You are not a member of channel {channelId}.");

		// Cap limit
		var effectiveLimit = Math.Min(limit, 100);

		// Build query: messages before the cursor (if provided)
		DateTime? cursorTime = null;
		if (before.HasValue)
		{
			cursorTime = await Db.Messages.IgnoreQueryFilters()
				.Where(m => m.Id == before.Value)
				.Select(m => (DateTime?)m.CreatedAt)
				.FirstOrDefaultAsync(ct);

			if (cursorTime is null)
				throw new KeyNotFoundException($"Cursor message {before} not found.");
		}

		var query = Db.Messages
			.Include(m => m.Author)
			.Include(m => m.ParentMessage).ThenInclude(p => p!.Author)
			.Include(m => m.Attachments)
			.Where(m => m.ChannelId == channelId);

		if (cursorTime.HasValue)
			query = query.Where(m => m.CreatedAt < cursorTime.Value);

		// Fetch one extra to know if there are more
		var messages = await query
			.OrderByDescending(m => m.CreatedAt)
			.Take(effectiveLimit + 1)
			.ToListAsync(ct);

		var hasMore = messages.Count > effectiveLimit;
		if (hasMore)
			messages = messages.Take(effectiveLimit).ToList();

		// Return oldest-first
		messages.Reverse();

		// Load mentions for all returned messages
		var messageIds = messages.Select(m => m.Id).ToList();
		var mentions = await Db.MessageMentions
			.Include(mm => mm.MentionedCollaborator)
			.Where(mm => messageIds.Contains(mm.MessageId))
			.ToListAsync(ct);

		var mentionsByMessage = mentions
			.GroupBy(mm => mm.MessageId)
			.ToDictionary(g => g.Key, g => g.ToList());

		return new MessagePageDto
		{
			Messages = messages.Select(m => MapToDto(m, mentionsByMessage.GetValueOrDefault(m.Id))).ToList(),
			HasMore = hasMore,
		};
	}

	// =====================================================================
	// POST MESSAGE
	// =====================================================================

	public async Task<MessageDto> PostMessageAsync(
		Guid channelId,
		PostMessageDto dto,
		Guid authorId,
		CancellationToken ct = default)
	{
		// Membership check
		var isMember = await Db.ChannelMembers
			.AnyAsync(m => m.ChannelId == channelId && m.CollaboratorId == authorId, ct);

		if (!isMember)
			throw new UnauthorizedAccessException($"You are not a member of channel {channelId}.");

		var author = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == authorId, ct)
			?? throw new KeyNotFoundException($"Author {authorId} not found.");

		// Validate parent if provided — must belong to the same channel.
		Message? parent = null;
		if (dto.ParentMessageId is Guid parentId)
		{
			parent = await Db.Messages
				.Include(p => p.Author)
				.FirstOrDefaultAsync(p => p.Id == parentId && p.ChannelId == channelId, ct);
			if (parent is null)
				throw new KeyNotFoundException($"Parent message {parentId} not found in channel {channelId}.");
		}

		// Allow empty body when attachments are present.
		var hasAttachments = dto.Attachments is { Count: > 0 };
		if (string.IsNullOrWhiteSpace(dto.Body) && !hasAttachments)
			throw new ArgumentException("Message must have a body or at least one attachment.");

		var bodyText = dto.Body ?? string.Empty;
		var message = new Message
		{
			Id = Guid.CreateVersion7(),
			ChannelId = channelId,
			AuthorId = authorId,
			Body = Protector.Protect(bodyText), // encrypted at rest; mentions/preview below use the plaintext
			ParentMessageId = parent?.Id,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Messages.Add(message);

		var attachmentDtos = new List<MessageAttachmentDto>();
		if (hasAttachments)
		{
			foreach (var a in dto.Attachments)
			{
				var att = new MessageAttachment
				{
					Id = Guid.CreateVersion7(),
					MessageId = message.Id,
					Kind = a.Kind,
					// Private objects store the key (URL is re-signed on read); legacy/public store the URL.
					Url = a.StorageKey is { Length: > 0 } ? string.Empty : a.Url,
					StorageKey = a.StorageKey,
					Mime = a.Mime,
					OriginalName = a.OriginalName ?? string.Empty,
					SizeBytes = a.SizeBytes,
					DurationSeconds = a.DurationSeconds,
					CreatedAt = DateTime.UtcNow,
				};
				Db.MessageAttachments.Add(att);
				attachmentDtos.Add(new MessageAttachmentDto
				{
					Id = att.Id, Kind = att.Kind, Url = ResolveAttachmentUrl(att), Mime = att.Mime,
					OriginalName = att.OriginalName, SizeBytes = att.SizeBytes, DurationSeconds = att.DurationSeconds,
				});
			}
		}

		await Db.SaveChangesAsync(ct);

		// Parse mentions, filter to channel members (not the author), persist rows + notifications
		var mentionedIds = MentionParser.ExtractCollaboratorIds(dto.Body);
		var mentionDtos = new List<MessageMentionDto>();

		if (mentionedIds.Count > 0)
		{
			// Get members of this channel
			var memberIds = await Db.ChannelMembers
				.Where(m => m.ChannelId == channelId)
				.Select(m => m.CollaboratorId)
				.ToListAsync(ct);

			var eligibleIds = mentionedIds
				.Where(id => id != authorId && memberIds.Contains(id))
				.ToList();

			foreach (var recipientId in eligibleIds)
			{
				var mention = new MessageMention
				{
					Id = Guid.CreateVersion7(),
					MessageId = message.Id,
					MentionedCollaboratorId = recipientId,
					CreatedAt = DateTime.UtcNow,
				};
				Db.MessageMentions.Add(mention);
			}

			if (eligibleIds.Count > 0)
				await Db.SaveChangesAsync(ct);

			// Load mentioned collaborator names for the DTO
			var mentionedCollaborators = await Db.Collaborators
				.Where(c => eligibleIds.Contains(c.Id))
				.Select(c => new { c.Id, c.FullName })
				.ToListAsync(ct);

			mentionDtos = mentionedCollaborators.Select(c => new MessageMentionDto
			{
				MentionedCollaboratorId = c.Id,
				MentionedCollaboratorName = c.FullName,
			}).ToList();

			// Emit notifications — strip mention tokens so the snippet shows @Name, not raw ids.
			// Single batched insert + parallel SignalR broadcast: a 10-mention message
			// went from ~10 sequential SaveChanges to one.
			var plainBody = MentionParser.ToPlainText(dto.Body);
			var bodySnippet = plainBody.Length > 100 ? plainBody[..100] + "…" : plainBody;
			await NotificationService.CreateManyAsync(
				eligibleIds,
				NotificationKind.Mention,
				$"{author.FullName} mentioned you",
				bodySnippet,
				"channel",
				channelId,
				authorId,
				ct);
		}

		// WhatsApp-style OS push to the other channel members (excludes the author and anyone
		// already @mentioned — they got the mention push). No bell-Notification entry, just push.
		try
		{
			var members = await Db.ChannelMembers
				.Where(m => m.ChannelId == channelId && m.CollaboratorId != authorId && !mentionedIds.Contains(m.CollaboratorId))
				.Select(m => new { m.CollaboratorId, m.IsMuted })
				.ToListAsync(ct);

			// Skip muted members, and skip anyone currently viewing this channel (their
			// presence connection has the channel active — they'll see it live).
			var pushRecipients = members
				.Where(m => !m.IsMuted && !Presence.IsActive(m.CollaboratorId, channelId))
				.Select(m => m.CollaboratorId)
				.ToList();

			if (pushRecipients.Count > 0)
			{
				var channel = await Db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId, ct);
				var isDm = channel?.Kind == ChannelKind.DirectMessage;
				var preview = BuildPushPreview(dto.Body, hasAttachments);
				var title = isDm ? author.FullName : channel?.Name ?? "WAAO";
				var pushBody = isDm ? preview : $"{author.FullName}: {preview}";
				var url = $"/messages?channel={channelId}";
				foreach (var recipientId in pushRecipients)
					await Push.SendToCollaboratorAsync(recipientId, title, pushBody, url, ct);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "Failed to send message push for channel {Channel}.", channelId);
		}

		return new MessageDto
		{
			Id = message.Id,
			ChannelId = message.ChannelId,
			AuthorId = message.AuthorId,
			AuthorName = author.FullName,
			AuthorPhotoUrl = author.PhotoUrl,
			Body = bodyText,
			CreatedAtUtc = message.CreatedAt,
			EditedAtUtc = null,
			ParentMessageId = parent?.Id,
			ParentPreview = parent is null ? null : new ParentMessagePreviewDto
			{
				Id = parent.Id,
				AuthorId = parent.AuthorId,
				AuthorName = parent.Author?.FullName ?? string.Empty,
				Body = MentionParser.ToPlainText(Protector.Unprotect(parent.Body) ?? string.Empty),
			},
			Mentions = mentionDtos,
			Attachments = attachmentDtos,
		};
	}

	// =====================================================================
	// EDIT MESSAGE
	// =====================================================================

	public async Task<MessageDto> EditMessageAsync(
		Guid channelId,
		Guid messageId,
		EditMessageDto dto,
		Guid callerId,
		CancellationToken ct = default)
	{
		var message = await Db.Messages
			.Include(m => m.Author)
			.Include(m => m.ParentMessage).ThenInclude(p => p!.Author)
			.Include(m => m.Attachments)
			.FirstOrDefaultAsync(m => m.Id == messageId && m.ChannelId == channelId, ct)
			?? throw new KeyNotFoundException($"Message {messageId} not found in channel {channelId}.");

		if (message.AuthorId != callerId)
			throw new UnauthorizedAccessException("Only the author can edit this message.");

		var newBody = (dto.Body ?? string.Empty).Trim();
		if (newBody.Length == 0)
			throw new ArgumentException("Message body cannot be empty.");
		if (newBody.Length > 4000)
			newBody = newBody[..4000];

		message.Body = Protector.Protect(newBody);
		message.EditedAtUtc = DateTime.UtcNow;
		message.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var mentions = await Db.MessageMentions
			.Include(mm => mm.MentionedCollaborator)
			.Where(mm => mm.MessageId == messageId)
			.ToListAsync(ct);

		return MapToDto(message, mentions);
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private static string BuildPushPreview(string? body, bool hasAttachments)
	{
		var plain = MentionParser.ToPlainText(body ?? string.Empty).Trim();
		if (string.IsNullOrEmpty(plain))
			return hasAttachments ? "📎" : "";
		return plain.Length > 120 ? plain[..120] + "…" : plain;
	}

	public async Task<IReadOnlyList<RecentMessageDto>> GetRecentAcrossMyChannelsAsync(Guid callerId, int limit, CancellationToken ct = default)
	{
		var effectiveLimit = Math.Clamp(limit, 1, 25);

		var myChannelIds = await Db.ChannelMembers
			.AsNoTracking()
			.Where(m => m.CollaboratorId == callerId)
			.Select(m => m.ChannelId)
			.ToListAsync(ct);

		if (myChannelIds.Count == 0) return [];

		var messages = await Db.Messages
			.AsNoTracking()
			.Include(m => m.Author)
			.Include(m => m.Channel)
			.Where(m => myChannelIds.Contains(m.ChannelId))
			.OrderByDescending(m => m.CreatedAt)
			.Take(effectiveLimit)
			.ToListAsync(ct);

		return messages.Select(m => new RecentMessageDto
		{
			Id = m.Id,
			ChannelId = m.ChannelId,
			ChannelName = m.Channel.Name,
			ChannelKind = m.Channel.Kind,
			AuthorId = m.AuthorId,
			AuthorName = m.Author.FullName,
			AuthorPhotoUrl = m.Author.PhotoUrl,
			Body = MentionParser.ToPlainText(Protector.Unprotect(m.Body) ?? string.Empty),
			CreatedAtUtc = m.CreatedAt,
			EditedAtUtc = m.EditedAtUtc,
		}).ToList();
	}

	private MessageDto MapToDto(Message m, List<MessageMention>? mentions) => new()
	{
		Id = m.Id,
		ChannelId = m.ChannelId,
		AuthorId = m.AuthorId,
		AuthorName = m.Author.FullName,
		AuthorPhotoUrl = m.Author.PhotoUrl,
		Body = Protector.Unprotect(m.Body) ?? string.Empty,
		CreatedAtUtc = m.CreatedAt,
		EditedAtUtc = m.EditedAtUtc,
		ParentMessageId = m.ParentMessageId,
		ParentPreview = m.ParentMessage is null ? null : new ParentMessagePreviewDto
		{
			Id = m.ParentMessage.Id,
			AuthorId = m.ParentMessage.AuthorId,
			AuthorName = m.ParentMessage.Author?.FullName ?? string.Empty,
			Body = MentionParser.ToPlainText(Protector.Unprotect(m.ParentMessage.Body) ?? string.Empty),
		},
		Mentions = mentions?.Select(mm => new MessageMentionDto
		{
			MentionedCollaboratorId = mm.MentionedCollaboratorId,
			MentionedCollaboratorName = mm.MentionedCollaborator?.FullName ?? string.Empty,
		}).ToList() ?? [],
		Attachments = m.Attachments?.Select(a => new MessageAttachmentDto
		{
			Id = a.Id, Kind = a.Kind, Url = ResolveAttachmentUrl(a), Mime = a.Mime,
			OriginalName = a.OriginalName, SizeBytes = a.SizeBytes, DurationSeconds = a.DurationSeconds,
		}).ToList() ?? [],
	};
}
