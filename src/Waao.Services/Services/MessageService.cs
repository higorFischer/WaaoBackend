using Microsoft.EntityFrameworkCore;
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
	INotificationService NotificationService) : IMessageService
{
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

		var message = new Message
		{
			Id = Guid.CreateVersion7(),
			ChannelId = channelId,
			AuthorId = authorId,
			Body = dto.Body,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Messages.Add(message);
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
			var plainBody = MentionParser.ToPlainText(dto.Body);
			var bodySnippet = plainBody.Length > 100 ? plainBody[..100] + "…" : plainBody;
			foreach (var recipientId in eligibleIds)
			{
				await NotificationService.CreateAsync(
					recipientId,
					NotificationKind.Mention,
					$"{author.FullName} mentioned you",
					bodySnippet,
					"channel",
					channelId,
					authorId,
					ct);
			}
		}

		return new MessageDto
		{
			Id = message.Id,
			ChannelId = message.ChannelId,
			AuthorId = message.AuthorId,
			AuthorName = author.FullName,
			AuthorPhotoUrl = author.PhotoUrl,
			Body = message.Body,
			CreatedAtUtc = message.CreatedAt,
			Mentions = mentionDtos,
		};
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private static MessageDto MapToDto(Message m, List<MessageMention>? mentions) => new()
	{
		Id = m.Id,
		ChannelId = m.ChannelId,
		AuthorId = m.AuthorId,
		AuthorName = m.Author.FullName,
		AuthorPhotoUrl = m.Author.PhotoUrl,
		Body = m.Body,
		CreatedAtUtc = m.CreatedAt,
		Mentions = mentions?.Select(mm => new MessageMentionDto
		{
			MentionedCollaboratorId = mm.MentionedCollaboratorId,
			MentionedCollaboratorName = mm.MentionedCollaborator?.FullName ?? string.Empty,
		}).ToList() ?? [],
	};
}
