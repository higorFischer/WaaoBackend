using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Waao.Domain.Models.Entities.Calls;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Dtos.Calls;
using Waao.Services.Abstractions.Services;
using Waao.Services.Video;

namespace Waao.Services.Services.Calls;

public sealed class CallChannelService(
	WaaoDbContext Db,
	ICallPresenceTracker Presence,
	ILiveKitTokenService LiveKitTokenService,
	IOptions<LiveKitOptions> LiveKitOptions,
	ILogger<CallChannelService> Logger) : ICallChannelService
{
	public async Task<IReadOnlyList<CallChannelDto>> ListAsync(CancellationToken ct = default)
	{
		var channels = await Db.CallChannels.AsNoTracking()
			.Where(c => !c.IsArchived)
			.OrderBy(c => c.Position)
			.ThenBy(c => c.Name)
			.ToListAsync(ct);

		var snapshot = Presence.SnapshotAll();

		return channels.Select(c => ToDto(c, snapshot.TryGetValue(c.Id, out var p) ? p : [])).ToList();
	}

	public async Task<CallChannelDto> CreateAsync(CreateCallChannelDto dto, Guid creatorId, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Name is required.");

		var creator = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == creatorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {creatorId} not found.");

		var nextPos = (await Db.CallChannels.Where(c => !c.IsArchived).Select(c => (int?)c.Position).MaxAsync(ct) ?? -1) + 1;

		var entity = new CallChannel
		{
			Id = Guid.CreateVersion7(),
			Name = dto.Name.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#2A6B7E" : dto.ColorHex.Trim(),
			Position = nextPos,
			CreatedById = creatorId,
			CreatedByName = creator.FullName,
			CreatedAt = DateTime.UtcNow,
		};
		Db.CallChannels.Add(entity);
		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("CallChannel {Id} created by {Creator}.", entity.Id, creatorId);
		return ToDto(entity, []);
	}

	public async Task<CallChannelDto> UpdateAsync(Guid id, UpdateCallChannelDto dto, CancellationToken ct = default)
	{
		var entity = await Db.CallChannels.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"CallChannel {id} not found.");

		entity.Name = dto.Name.Trim();
		entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		entity.ColorHex = dto.ColorHex.Trim();
		entity.Position = dto.Position;
		entity.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return ToDto(entity, Presence.GetParticipants(id));
	}

	public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
	{
		var entity = await Db.CallChannels.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"CallChannel {id} not found.");
		entity.IsArchived = true;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<CallTokenDto> GetTokenAsync(Guid channelId, Guid callerId, CancellationToken ct = default)
	{
		var channel = await Db.CallChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == channelId && !c.IsArchived, ct)
			?? throw new KeyNotFoundException($"CallChannel {channelId} not found.");

		var caller = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == callerId, ct)
			?? throw new KeyNotFoundException($"Collaborator {callerId} not found.");

		var room = $"waao-call-{channel.Id:N}";
		var token = LiveKitTokenService.MintToken(new LiveKitTokenRequest
		{
			CollaboratorId = callerId,
			Name = caller.FullName,
			Room = room,
			Moderator = false,
		});

		return new CallTokenDto
		{
			Token = token,
			Url = LiveKitOptions.Value.Url,
			Room = room,
		};
	}

	private static CallChannelDto ToDto(CallChannel c, IReadOnlyList<CallParticipantDto> participants) => new()
	{
		Id = c.Id,
		Name = c.Name,
		Description = c.Description,
		ColorHex = c.ColorHex,
		Position = c.Position,
		CreatedById = c.CreatedById,
		CreatedByName = c.CreatedByName,
		Participants = participants,
	};
}
