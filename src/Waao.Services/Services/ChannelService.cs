using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Abstractions.Services;
using Waao.Services.Messaging;

namespace Waao.Services.Services;

public sealed class ChannelService(
	WaaoDbContext Db,
	INotificationService NotificationService,
	IMessageTextProtector Protector) : IChannelService
{
	// =====================================================================
	// LIST MY CHANNELS
	// =====================================================================

	public async Task<IReadOnlyList<ChannelDto>> ListMyChannelsAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var memberships = await Db.ChannelMembers
			.AsNoTracking()
			.Include(m => m.Channel)
			.Where(m => m.CollaboratorId == collaboratorId)
			.ToListAsync(ct);

		if (memberships.Count == 0) return [];

		var channelIds = memberships.Select(m => m.ChannelId).ToList();
		var lastReadIds = memberships
			.Where(m => m.LastReadMessageId.HasValue)
			.Select(m => m.LastReadMessageId!.Value)
			.Distinct()
			.ToList();

		// Last message per channel — single grouped query, project only what the
		// preview needs (not the full body — capped on render).
		var lastMessages = (await Db.Messages.AsNoTracking()
			.Where(msg => channelIds.Contains(msg.ChannelId))
			.GroupBy(msg => msg.ChannelId)
			.Select(g => new
			{
				ChannelId = g.Key,
				Body = g.OrderByDescending(m => m.CreatedAt).First().Body,
				CreatedAt = g.OrderByDescending(m => m.CreatedAt).First().CreatedAt,
			})
			.ToListAsync(ct))
			.ToDictionary(x => x.ChannelId, x => x);

		var memberCounts = (await Db.ChannelMembers.AsNoTracking()
			.Where(m => channelIds.Contains(m.ChannelId))
			.GroupBy(m => m.ChannelId)
			.Select(g => new { ChannelId = g.Key, Count = g.Count() })
			.ToListAsync(ct))
			.ToDictionary(x => x.ChannelId, x => x.Count);

		var lastReadByMsg = lastReadIds.Count == 0
			? new Dictionary<Guid, DateTime>()
			: (await Db.Messages.AsNoTracking().IgnoreQueryFilters()
				.Where(m => lastReadIds.Contains(m.Id))
				.Select(m => new { m.Id, m.CreatedAt })
				.ToListAsync(ct))
				.ToDictionary(x => x.Id, x => x.CreatedAt);

		var dmChannelIds = memberships
			.Where(m => m.Channel.Kind == ChannelKind.DirectMessage)
			.Select(m => m.ChannelId)
			.ToList();
		var otherByChannel = dmChannelIds.Count == 0
			? new Dictionary<Guid, ChannelMember>()
			: (await Db.ChannelMembers.AsNoTracking()
				.Include(m => m.Collaborator)
				.Where(m => dmChannelIds.Contains(m.ChannelId) && m.CollaboratorId != collaboratorId)
				.ToListAsync(ct))
				.ToDictionary(m => m.ChannelId, m => m);

		// Unread count is computed server-side with an indexed COUNT(*) per
		// channel-with-lastread (CreatedAt > lastRead) and (total) for the rest.
		// One round-trip total, no pulling-all-timestamps-into-memory.
		var membershipsWithLastRead = memberships
			.Where(m => m.LastReadMessageId.HasValue && lastReadByMsg.ContainsKey(m.LastReadMessageId!.Value))
			.Select(m => new { m.ChannelId, LastReadAt = lastReadByMsg[m.LastReadMessageId!.Value] })
			.ToList();
		var channelsWithoutLastRead = memberships
			.Where(m => !m.LastReadMessageId.HasValue || !lastReadByMsg.ContainsKey(m.LastReadMessageId!.Value))
			.Select(m => m.ChannelId)
			.ToList();

		var unreadByChannel = new Dictionary<Guid, int>(memberships.Count);
		if (channelsWithoutLastRead.Count > 0)
		{
			var totals = await Db.Messages.AsNoTracking()
				.Where(m => channelsWithoutLastRead.Contains(m.ChannelId))
				.GroupBy(m => m.ChannelId)
				.Select(g => new { ChannelId = g.Key, Count = g.Count() })
				.ToListAsync(ct);
			foreach (var t in totals) unreadByChannel[t.ChannelId] = t.Count;
		}
		foreach (var m in membershipsWithLastRead)
		{
			unreadByChannel[m.ChannelId] = await Db.Messages.AsNoTracking()
				.CountAsync(msg => msg.ChannelId == m.ChannelId && msg.CreatedAt > m.LastReadAt, ct);
		}

		var dtos = new List<ChannelDto>(memberships.Count);
		foreach (var membership in memberships)
		{
			var channel = membership.Channel;
			lastMessages.TryGetValue(channel.Id, out var lastMsg);

			ChannelMemberDto? otherMember = null;
			if (channel.Kind == ChannelKind.DirectMessage && otherByChannel.TryGetValue(channel.Id, out var other))
			{
				otherMember = new ChannelMemberDto
				{
					CollaboratorId = other.CollaboratorId,
					CollaboratorName = other.Collaborator.FullName,
					CollaboratorPhotoUrl = other.Collaborator.PhotoUrl,
					JoinedAt = other.JoinedAt,
				};
			}

			dtos.Add(new ChannelDto
			{
				Id = channel.Id,
				Name = channel.Name,
				Description = channel.Description,
				Kind = channel.Kind,
				Scope = channel.Scope,
				DepartmentId = channel.DepartmentId,
				CreatedById = channel.CreatedById,
				MemberCount = memberCounts.GetValueOrDefault(channel.Id, 0),
				IsMember = true,
				IsMuted = membership.IsMuted,
				UnreadCount = unreadByChannel.GetValueOrDefault(channel.Id, 0),
				LastMessagePreview = PreviewOf(Protector.Unprotect(lastMsg?.Body)),
				LastMessageAtUtc = lastMsg?.CreatedAt,
				OtherMember = otherMember,
			});
		}

		return dtos.OrderByDescending(d => d.LastMessageAtUtc ?? DateTime.MinValue).ToList();
	}

	// =====================================================================
	// LIST PUBLIC CHANNELS
	// =====================================================================

	public async Task<IReadOnlyList<ChannelDto>> ListPublicChannelsAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var myChannelIds = await Db.ChannelMembers
			.Where(m => m.CollaboratorId == collaboratorId)
			.Select(m => m.ChannelId)
			.ToListAsync(ct);

		var channels = await Db.Channels
			.Where(c => c.Kind == ChannelKind.Public && !myChannelIds.Contains(c.Id))
			.ToListAsync(ct);

		var channelIds = channels.Select(c => c.Id).ToList();

		var memberCounts = await Db.ChannelMembers
			.Where(m => channelIds.Contains(m.ChannelId))
			.GroupBy(m => m.ChannelId)
			.Select(g => new { ChannelId = g.Key, Count = g.Count() })
			.ToListAsync(ct);

		return channels.Select(c => new ChannelDto
		{
			Id = c.Id,
			Name = c.Name,
			Description = c.Description,
			Kind = c.Kind,
			Scope = c.Scope,
			DepartmentId = c.DepartmentId,
			CreatedById = c.CreatedById,
			MemberCount = memberCounts.FirstOrDefault(x => x.ChannelId == c.Id)?.Count ?? 0,
			IsMember = false,
			UnreadCount = 0,
		}).ToList();
	}

	// =====================================================================
	// CREATE CHANNEL
	// =====================================================================

	public async Task<ChannelDto> CreateChannelAsync(CreateChannelDto dto, Guid creatorId, CancellationToken ct = default)
	{
		var name = (dto.Name ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Channel name is required.");
		if (name.Length > 120) name = name[..120];

		// Reject duplicates by name across non-DM channels (case-insensitive).
		var nameLower = name.ToLower();
		var duplicate = await Db.Channels.AnyAsync(c =>
			c.Kind != ChannelKind.DirectMessage
			&& c.Name != null
			&& c.Name.ToLower() == nameLower, ct);
		if (duplicate)
			throw new InvalidOperationException($"A channel named \"{name}\" already exists.");

		var channel = new Channel
		{
			Id = Guid.CreateVersion7(),
			Name = name,
			Description = dto.Description,
			Kind = dto.Kind,
			Scope = ChannelScope.Custom,
			CreatedById = creatorId,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Channels.Add(channel);

		// Add creator as member
		var memberIds = dto.InitialMemberIds
			.Append(creatorId)
			.Distinct()
			.ToList();

		foreach (var memberId in memberIds)
		{
			Db.ChannelMembers.Add(new ChannelMember
			{
				Id = Guid.CreateVersion7(),
				ChannelId = channel.Id,
				CollaboratorId = memberId,
				JoinedAt = DateTime.UtcNow,
				CreatedAt = DateTime.UtcNow,
			});
		}

		await Db.SaveChangesAsync(ct);

		return await BuildChannelDtoAsync(channel.Id, creatorId, ct);
	}

	// =====================================================================
	// JOIN
	// =====================================================================

	public async Task<ChannelDto> JoinAsync(Guid channelId, Guid collaboratorId, CancellationToken ct = default)
	{
		var channel = await Db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
			?? throw new KeyNotFoundException($"Channel {channelId} not found.");

		if (channel.Kind == ChannelKind.Private)
			throw new UnauthorizedAccessException("Cannot join a private channel directly. Ask a member to add you.");

		if (channel.Kind == ChannelKind.DirectMessage)
			throw new UnauthorizedAccessException("Cannot join a DM channel directly.");

		var existing = await Db.ChannelMembers.IgnoreQueryFilters()
			.FirstOrDefaultAsync(m => m.ChannelId == channelId && m.CollaboratorId == collaboratorId, ct);

		if (existing is not null)
		{
			if (existing.IsDeleted)
			{
				existing.IsDeleted = false;
				existing.DeletedAt = null;
				existing.JoinedAt = DateTime.UtcNow;
				existing.UpdatedAt = DateTime.UtcNow;
				await Db.SaveChangesAsync(ct);
			}

			return await BuildChannelDtoAsync(channelId, collaboratorId, ct);
		}

		Db.ChannelMembers.Add(new ChannelMember
		{
			Id = Guid.CreateVersion7(),
			ChannelId = channelId,
			CollaboratorId = collaboratorId,
			JoinedAt = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
		});

		await Db.SaveChangesAsync(ct);

		return await BuildChannelDtoAsync(channelId, collaboratorId, ct);
	}

	// =====================================================================
	// LEAVE
	// =====================================================================

	public async Task LeaveAsync(Guid channelId, Guid collaboratorId, CancellationToken ct = default)
	{
		var member = await Db.ChannelMembers
			.FirstOrDefaultAsync(m => m.ChannelId == channelId && m.CollaboratorId == collaboratorId, ct)
			?? throw new KeyNotFoundException($"You are not a member of channel {channelId}.");

		member.IsDeleted = true;
		member.DeletedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// ADD MEMBER
	// =====================================================================

	public async Task<ChannelDto> AddMemberAsync(Guid channelId, Guid collaboratorId, Guid actorId, CancellationToken ct = default)
	{
		// Actor must be a member
		var actorMembership = await Db.ChannelMembers
			.AnyAsync(m => m.ChannelId == channelId && m.CollaboratorId == actorId, ct);

		if (!actorMembership)
			throw new UnauthorizedAccessException($"You must be a member of channel {channelId} to add others.");

		var channel = await Db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
			?? throw new KeyNotFoundException($"Channel {channelId} not found.");

		// Check if already a member (re-activate if soft-deleted)
		var existing = await Db.ChannelMembers.IgnoreQueryFilters()
			.FirstOrDefaultAsync(m => m.ChannelId == channelId && m.CollaboratorId == collaboratorId, ct);

		if (existing is not null)
		{
			if (existing.IsDeleted)
			{
				existing.IsDeleted = false;
				existing.DeletedAt = null;
				existing.JoinedAt = DateTime.UtcNow;
				existing.UpdatedAt = DateTime.UtcNow;
				await Db.SaveChangesAsync(ct);
			}

			return await BuildChannelDtoAsync(channelId, actorId, ct);
		}

		Db.ChannelMembers.Add(new ChannelMember
		{
			Id = Guid.CreateVersion7(),
			ChannelId = channelId,
			CollaboratorId = collaboratorId,
			JoinedAt = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
		});

		await Db.SaveChangesAsync(ct);

		// Notify the newly added member (ChannelInvite) — skip self-joins (actorId == collaboratorId)
		if (collaboratorId != actorId)
		{
			var channelName = channel.Name ?? "a channel";
			await NotificationService.CreateAsync(
				collaboratorId,
				NotificationKind.ChannelInvite,
				$"You were added to #{channelName}",
				$"You've been added to the channel #{channelName}.",
				"channel",
				channelId,
				actorId,
				ct);
		}

		return await BuildChannelDtoAsync(channelId, actorId, ct);
	}

	// =====================================================================
	// OPEN DIRECT MESSAGE (find-or-create, never duplicate)
	// =====================================================================

	public async Task<ChannelDto> OpenDirectMessageAsync(Guid otherCollaboratorId, Guid callerId, CancellationToken ct = default)
	{
		// Find any existing DM between the two collaborators
		// A DM channel is a DirectMessage kind with exactly both as members
		var callerDmChannelIds = await Db.ChannelMembers
			.Include(m => m.Channel)
			.Where(m => m.CollaboratorId == callerId && m.Channel.Kind == ChannelKind.DirectMessage)
			.Select(m => m.ChannelId)
			.ToListAsync(ct);

		if (callerDmChannelIds.Count > 0)
		{
			var existingDmId = await Db.ChannelMembers
				.Where(m => callerDmChannelIds.Contains(m.ChannelId) && m.CollaboratorId == otherCollaboratorId)
				.Select(m => (Guid?)m.ChannelId)
				.FirstOrDefaultAsync(ct);

			if (existingDmId.HasValue)
				return await BuildChannelDtoAsync(existingDmId.Value, callerId, ct);
		}

		// Create new DM channel
		var channel = new Channel
		{
			Id = Guid.CreateVersion7(),
			Kind = ChannelKind.DirectMessage,
			Scope = ChannelScope.Custom,
			CreatedById = callerId,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Channels.Add(channel);

		Db.ChannelMembers.Add(new ChannelMember
		{
			Id = Guid.CreateVersion7(),
			ChannelId = channel.Id,
			CollaboratorId = callerId,
			JoinedAt = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
		});

		Db.ChannelMembers.Add(new ChannelMember
		{
			Id = Guid.CreateVersion7(),
			ChannelId = channel.Id,
			CollaboratorId = otherCollaboratorId,
			JoinedAt = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
		});

		await Db.SaveChangesAsync(ct);

		return await BuildChannelDtoAsync(channel.Id, callerId, ct);
	}

	// =====================================================================
	// MARK READ
	// =====================================================================

	public async Task MarkReadAsync(Guid channelId, MarkReadDto dto, Guid collaboratorId, CancellationToken ct = default)
	{
		var member = await Db.ChannelMembers
			.FirstOrDefaultAsync(m => m.ChannelId == channelId && m.CollaboratorId == collaboratorId, ct)
			?? throw new UnauthorizedAccessException($"You are not a member of channel {channelId}.");

		member.LastReadMessageId = dto.LastReadMessageId;
		member.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// SET MUTED
	// =====================================================================

	public async Task<ChannelDto> SetMutedAsync(Guid channelId, Guid collaboratorId, bool muted, CancellationToken ct = default)
	{
		var member = await Db.ChannelMembers
			.FirstOrDefaultAsync(m => m.ChannelId == channelId && m.CollaboratorId == collaboratorId, ct)
			?? throw new KeyNotFoundException($"You are not a member of channel {channelId}.");

		member.IsMuted = muted;
		member.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		return await BuildChannelDtoAsync(channelId, collaboratorId, ct);
	}

	// =====================================================================
	// GET MEMBERS
	// =====================================================================

	public async Task<IReadOnlyList<ChannelMemberDto>> GetMembersAsync(Guid channelId, Guid callerId, CancellationToken ct = default)
	{
		var isMember = await Db.ChannelMembers
			.AnyAsync(m => m.ChannelId == channelId && m.CollaboratorId == callerId, ct);

		if (!isMember)
			throw new UnauthorizedAccessException($"You are not a member of channel {channelId}.");

		return await Db.ChannelMembers
			.Include(m => m.Collaborator)
			.Where(m => m.ChannelId == channelId)
			.Select(m => new ChannelMemberDto
			{
				CollaboratorId = m.CollaboratorId,
				CollaboratorName = m.Collaborator.FullName,
				CollaboratorPhotoUrl = m.Collaborator.PhotoUrl,
				JoinedAt = m.JoinedAt,
			})
			.ToListAsync(ct);
	}

	// =====================================================================
	// UPDATE CHANNEL
	// =====================================================================

	public async Task<ChannelDto> UpdateChannelAsync(Guid channelId, UpdateChannelDto dto, Guid callerId, CancellationToken ct = default)
	{
		var channel = await Db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
			?? throw new KeyNotFoundException($"Channel {channelId} not found.");

		if (channel.Kind == ChannelKind.DirectMessage)
			throw new InvalidOperationException("Direct message channels cannot be edited.");

		if (!await CanManageChannelAsync(channel, callerId, ct))
			throw new UnauthorizedAccessException($"Caller {callerId} cannot manage channel {channelId}.");

		if (dto.Name is not null)
		{
			var newName = dto.Name.Trim();
			if (newName.Length == 0) throw new ArgumentException("Channel name is required.");
			if (newName.Length > 120) newName = newName[..120];

			var lower = newName.ToLower();
			var collides = await Db.Channels.AnyAsync(c =>
				c.Id != channel.Id
				&& c.Kind != ChannelKind.DirectMessage
				&& c.Name != null
				&& c.Name.ToLower() == lower, ct);
			if (collides)
				throw new InvalidOperationException($"A channel named \"{newName}\" already exists.");

			channel.Name = newName;
		}
		if (dto.Description is not null) channel.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		if (dto.Kind is ChannelKind newKind)
		{
			if (newKind == ChannelKind.DirectMessage)
				throw new InvalidOperationException("Cannot change channel kind to DirectMessage.");
			channel.Kind = newKind;
		}
		channel.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		return await BuildChannelDtoAsync(channelId, callerId, ct);
	}

	// =====================================================================
	// REMOVE MEMBER
	// =====================================================================

	public async Task RemoveMemberAsync(Guid channelId, Guid collaboratorId, Guid actorId, CancellationToken ct = default)
	{
		var channel = await Db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
			?? throw new KeyNotFoundException($"Channel {channelId} not found.");

		if (channel.Kind == ChannelKind.DirectMessage)
			throw new InvalidOperationException("Cannot remove members from a direct message channel.");

		if (!await CanManageChannelAsync(channel, actorId, ct))
			throw new UnauthorizedAccessException($"Caller {actorId} cannot manage channel {channelId}.");

		if (collaboratorId == channel.CreatedById)
			throw new InvalidOperationException("Cannot remove the channel creator.");

		var member = await Db.ChannelMembers
			.FirstOrDefaultAsync(m => m.ChannelId == channelId && m.CollaboratorId == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} is not a member of channel {channelId}.");

		member.IsDeleted = true;
		member.DeletedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// DELETE CHANNEL
	// =====================================================================

	public async Task DeleteChannelAsync(Guid channelId, Guid callerId, CancellationToken ct = default)
	{
		var channel = await Db.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
			?? throw new KeyNotFoundException($"Channel {channelId} not found.");

		if (channel.Kind == ChannelKind.DirectMessage)
			throw new InvalidOperationException("Direct message channels cannot be deleted.");

		if (!await CanManageChannelAsync(channel, callerId, ct))
			throw new UnauthorizedAccessException($"Caller {callerId} cannot manage channel {channelId}.");

		channel.IsDeleted = true;
		channel.DeletedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
	}

	// Creator or Admin can manage a channel.
	private async Task<bool> CanManageChannelAsync(Channel channel, Guid callerId, CancellationToken ct)
	{
		if (channel.CreatedById == callerId) return true;
		return await Db.Collaborators
			.AnyAsync(c => c.Id == callerId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
	}

	// =====================================================================
	// INTERNAL HELPER: create channel (used by seed)
	// =====================================================================

	public async Task<Channel> CreateChannelEntityAsync(
		string? name,
		ChannelKind kind,
		ChannelScope scope,
		Guid creatorId,
		Guid? departmentId,
		IEnumerable<Guid> memberIds,
		CancellationToken ct = default)
	{
		var channel = new Channel
		{
			Id = Guid.CreateVersion7(),
			Name = name,
			Kind = kind,
			Scope = scope,
			DepartmentId = departmentId,
			CreatedById = creatorId,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Channels.Add(channel);

		foreach (var memberId in memberIds.Distinct())
		{
			Db.ChannelMembers.Add(new ChannelMember
			{
				Id = Guid.CreateVersion7(),
				ChannelId = channel.Id,
				CollaboratorId = memberId,
				JoinedAt = DateTime.UtcNow,
				CreatedAt = DateTime.UtcNow,
			});
		}

		await Db.SaveChangesAsync(ct);
		return channel;
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task<ChannelDto> BuildChannelDtoAsync(Guid channelId, Guid callerId, CancellationToken ct)
	{
		var channel = await Db.Channels.FirstAsync(c => c.Id == channelId, ct);

		var members = await Db.ChannelMembers
			.Include(m => m.Collaborator)
			.Where(m => m.ChannelId == channelId)
			.ToListAsync(ct);

		var callerMembership = members.FirstOrDefault(m => m.CollaboratorId == callerId);
		var isMember = callerMembership is not null;

		// Unread count
		int unreadCount = 0;
		if (isMember)
		{
			if (callerMembership!.LastReadMessageId is null)
			{
				unreadCount = await Db.Messages.CountAsync(m => m.ChannelId == channelId, ct);
			}
			else
			{
				var lastRead = await Db.Messages.IgnoreQueryFilters()
					.Where(m => m.Id == callerMembership.LastReadMessageId)
					.Select(m => (DateTime?)m.CreatedAt)
					.FirstOrDefaultAsync(ct);

				if (lastRead.HasValue)
					unreadCount = await Db.Messages.CountAsync(m => m.ChannelId == channelId && m.CreatedAt > lastRead.Value, ct);
			}
		}

		// Last message
		var lastMsg = await Db.Messages
			.Where(m => m.ChannelId == channelId)
			.OrderByDescending(m => m.CreatedAt)
			.Select(m => new { m.Body, m.CreatedAt })
			.FirstOrDefaultAsync(ct);

		// DM: other member
		ChannelMemberDto? otherMember = null;
		if (channel.Kind == ChannelKind.DirectMessage)
		{
			var other = members.FirstOrDefault(m => m.CollaboratorId != callerId);
			if (other is not null)
			{
				otherMember = new ChannelMemberDto
				{
					CollaboratorId = other.CollaboratorId,
					CollaboratorName = other.Collaborator.FullName,
					CollaboratorPhotoUrl = other.Collaborator.PhotoUrl,
					JoinedAt = other.JoinedAt,
				};
			}
		}

		return new ChannelDto
		{
			Id = channel.Id,
			Name = channel.Name,
			Description = channel.Description,
			Kind = channel.Kind,
			Scope = channel.Scope,
			DepartmentId = channel.DepartmentId,
			CreatedById = channel.CreatedById,
			MemberCount = members.Count,
			IsMember = isMember,
			IsMuted = callerMembership?.IsMuted ?? false,
			UnreadCount = unreadCount,
			LastMessagePreview = PreviewOf(lastMsg?.Body),
			LastMessageAtUtc = lastMsg?.CreatedAt,
			OtherMember = otherMember,
		};
	}

	// Channel-list preview: mention tokens collapse to @Name, then truncate.
	private static string? PreviewOf(string? body)
	{
		if (string.IsNullOrEmpty(body)) return null;
		var plain = MentionParser.ToPlainText(body);
		return plain.Length > 80 ? plain[..80] + "…" : plain;
	}
}
