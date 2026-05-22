using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class MessageService(WaaoDbContext Db) : IMessageService
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

		return new MessagePageDto
		{
			Messages = messages.Select(MapToDto).ToList(),
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

		return new MessageDto
		{
			Id = message.Id,
			ChannelId = message.ChannelId,
			AuthorId = message.AuthorId,
			AuthorName = author.FullName,
			AuthorPhotoUrl = author.PhotoUrl,
			Body = message.Body,
			CreatedAtUtc = message.CreatedAt,
		};
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private static MessageDto MapToDto(Message m) => new()
	{
		Id = m.Id,
		ChannelId = m.ChannelId,
		AuthorId = m.AuthorId,
		AuthorName = m.Author.FullName,
		AuthorPhotoUrl = m.Author.PhotoUrl,
		Body = m.Body,
		CreatedAtUtc = m.CreatedAt,
	};
}
