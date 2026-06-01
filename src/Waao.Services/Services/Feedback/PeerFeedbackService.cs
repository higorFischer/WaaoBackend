using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.Feedback;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Feedback;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.Feedback;

public sealed class PeerFeedbackService(
	WaaoDbContext Db,
	INotificationService NotificationService,
	ILogger<PeerFeedbackService> Logger) : IPeerFeedbackService
{
	public async Task<PeerFeedbackDto> GiveAsync(GivePeerFeedbackDto dto, Guid giverId, CancellationToken ct = default)
	{
		if (giverId == dto.RecipientId)
			throw new InvalidOperationException("You can't give feedback to yourself.");
		if (string.IsNullOrWhiteSpace(dto.Message))
			throw new InvalidOperationException("Message is required.");

		var giver = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == giverId, ct)
			?? throw new KeyNotFoundException($"Collaborator {giverId} not found.");
		var recipient = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.RecipientId, ct)
			?? throw new KeyNotFoundException($"Collaborator {dto.RecipientId} not found.");

		var entity = new PeerFeedback
		{
			Id = Guid.CreateVersion7(),
			GiverId = giverId,
			GiverName = giver.FullName,
			RecipientId = dto.RecipientId,
			RecipientName = recipient.FullName,
			Category = dto.Category,
			Message = dto.Message.Trim(),
			IsAnonymous = dto.IsAnonymous,
			CreatedAt = DateTime.UtcNow,
		};
		Db.PeerFeedbacks.Add(entity);
		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("PeerFeedback {Id} from {Giver} to {Recipient} (anon={Anon}).",
			entity.Id, giverId, dto.RecipientId, dto.IsAnonymous);

		// Notify the recipient. The bell entry hides the giver if anonymous.
		var title = dto.IsAnonymous ? "Você recebeu um feedback" : $"{giver.FullName} te enviou um feedback";
		var body = entity.Message.Length > 100 ? entity.Message[..100] + "…" : entity.Message;
		await NotificationService.CreateAsync(
			dto.RecipientId,
			NotificationKind.SystemAnnouncement,
			title,
			body,
			"peer-feedback",
			entity.Id,
			dto.IsAnonymous ? null : giverId,
			ct);

		// Return without exposing the giver if anonymous (the giver's own call though — return real for them).
		return ToDto(entity, callerIsRecipient: false, callerIsStaff: false);
	}

	public async Task<IReadOnlyList<PeerFeedbackDto>> ListReceivedAsync(Guid collaboratorId, Guid callerId, bool callerIsStaff, CancellationToken ct = default)
	{
		var canSeeGivers = callerIsStaff;
		// The recipient themselves can see non-anonymous givers but never the anonymous ones.
		var rows = await Db.PeerFeedbacks
			.AsNoTracking()
			.Include(p => p.Giver)
			.Where(p => p.RecipientId == collaboratorId)
			.OrderByDescending(p => p.CreatedAt)
			.ToListAsync(ct);

		return rows.Select(r => ToDto(r, callerIsRecipient: callerId == collaboratorId, callerIsStaff: canSeeGivers)).ToList();
	}

	public async Task<IReadOnlyList<PeerFeedbackDto>> ListGivenAsync(Guid collaboratorId, Guid callerId, CancellationToken ct = default)
	{
		// Only the giver themselves can list "given" — keeps Anonymous feedback truly anonymous to peers.
		if (callerId != collaboratorId)
			throw new UnauthorizedAccessException("You can only list the feedback you gave.");

		var rows = await Db.PeerFeedbacks
			.AsNoTracking()
			.Include(p => p.Giver)
			.Where(p => p.GiverId == collaboratorId)
			.OrderByDescending(p => p.CreatedAt)
			.ToListAsync(ct);

		// The giver always sees their own identity.
		return rows.Select(r => ToDto(r, callerIsRecipient: false, callerIsStaff: true)).ToList();
	}

	public async Task<PeerFeedbackDto> AcknowledgeAsync(Guid id, Guid callerId, CancellationToken ct = default)
	{
		var entity = await Db.PeerFeedbacks.FirstOrDefaultAsync(p => p.Id == id, ct)
			?? throw new KeyNotFoundException($"PeerFeedback {id} not found.");

		if (entity.RecipientId != callerId)
			throw new UnauthorizedAccessException("Only the recipient can acknowledge feedback.");

		entity.Acknowledged = true;
		entity.AcknowledgedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToDto(entity, callerIsRecipient: true, callerIsStaff: false);
	}

	private static PeerFeedbackDto ToDto(PeerFeedback p, bool callerIsRecipient, bool callerIsStaff)
	{
		var revealGiver = !p.IsAnonymous || callerIsStaff;
		return new PeerFeedbackDto
		{
			Id = p.Id,
			GiverId = revealGiver ? p.GiverId : null,
			GiverName = revealGiver ? p.GiverName : null,
			GiverPhotoUrl = revealGiver ? p.Giver?.PhotoUrl : null,
			RecipientId = p.RecipientId,
			RecipientName = p.RecipientName,
			Category = p.Category,
			Message = p.Message,
			IsAnonymous = p.IsAnonymous,
			Acknowledged = p.Acknowledged,
			AcknowledgedAtUtc = p.AcknowledgedAt,
			CreatedAtUtc = p.CreatedAt,
		};
	}
}
