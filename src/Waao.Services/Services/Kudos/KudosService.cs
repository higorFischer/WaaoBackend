using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Kudos;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Kudos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;

namespace Waao.Services.Services.Kudos;

public sealed class KudosService(
	WaaoDbContext Db,
	INotificationService NotificationService,
	GamificationEngine Gamification,
	BadgeEvaluator Badges,
	IValidator<GiveKudoDto> GiveKudoValidator,
	ILogger<KudosService> Logger) : IKudosService
{
	private const int KudoXp = 25;

	public async Task<KudoDto> GiveAsync(GiveKudoDto dto, Guid giverId, CancellationToken ct = default)
	{
		await GiveKudoValidator.ValidateAndThrowAsync(dto, ct);

		var giver = await Db.Collaborators
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == giverId, ct)
			?? throw new KeyNotFoundException($"Collaborator {giverId} not found.");

		// Distinct recipient ids, excluding the giver (you can't kudos yourself)
		var recipientIds = dto.RecipientIds
			.Distinct()
			.Where(id => id != giverId)
			.ToList();

		if (recipientIds.Count == 0)
			throw new ArgumentException("You cannot give a kudo only to yourself. Provide at least one other recipient.");

		// Load recipient collaborators
		var recipients = await Db.Collaborators
			.AsNoTracking()
			.Where(c => recipientIds.Contains(c.Id) && !c.IsDeleted)
			.ToListAsync(ct);

		var kudo = new Kudo
		{
			Id = Guid.CreateVersion7(),
			GiverId = giverId,
			GiverName = giver.FullName,
			GiverPhotoUrl = giver.PhotoUrl,
			Value = dto.Value,
			Message = dto.Message,
			CreatedAt = DateTime.UtcNow,
		};

		foreach (var recipient in recipients)
		{
			kudo.Recipients.Add(new KudoRecipient
			{
				Id = Guid.CreateVersion7(),
				KudoId = kudo.Id,
				CollaboratorId = recipient.Id,
				CollaboratorName = recipient.FullName,
				CollaboratorPhotoUrl = recipient.PhotoUrl,
				CreatedAt = DateTime.UtcNow,
			});
		}

		Db.Kudos.Add(kudo);
		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Kudo {Id} given by {GiverId} to {Count} recipient(s).", kudo.Id, giverId, recipients.Count);

		// Per-recipient: award XP + record career event + notify (best-effort)
		foreach (var recipient in recipients)
		{
			try
			{
				// Award XP via GamificationEngine (records XpTransaction + updates TotalXp + CurrentLevel)
				await Gamification.RecordAsync(
					recipient.Id,
					KudoXp,
					XpSource.PeerKudos,
					$"Kudo received from {giver.FullName}: {dto.Value}",
					kudo.Id,
					"Kudo",
					ct);

				// Record a Kudos CareerEvent so BadgeEvaluator.FIRST_KUDOS / TEN_KUDOS fire
				Db.CareerEvents.Add(new CareerEvent
				{
					Id = Guid.CreateVersion7(),
					CollaboratorId = recipient.Id,
					Type = CareerEventType.Kudos,
					EventDate = DateOnly.FromDateTime(DateTime.UtcNow),
					Title = $"Kudo received: {dto.Value}",
					Notes = dto.Message.Length > 80 ? dto.Message[..80] : dto.Message,
					XpAwarded = KudoXp,
					CreatedAt = DateTime.UtcNow,
				});
				await Db.SaveChangesAsync(ct);

				// Evaluate badges (FIRST_KUDOS / TEN_KUDOS) and award any newly unlocked ones
				var newBadges = await Badges.EvaluateAsync(recipient.Id, ct);
				if (newBadges.Count > 0)
					await Db.SaveChangesAsync(ct);

				// Notify recipient
				var messagePreview = dto.Message.Length > 80 ? $"{dto.Message[..77]}..." : dto.Message;
				var notifTitle = $"{giver.FullName} reconheceu você 🎉";
				var notifBody = $"{dto.Value}: {messagePreview}";
				await NotificationService.CreateAsync(recipient.Id, NotificationKind.KudoReceived, notifTitle, notifBody, "kudos", kudo.Id, giverId, ct);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Failed to award XP/career-event/badge/notify for kudo {KudoId} recipient {RecipientId}.", kudo.Id, recipient.Id);
			}
		}

		return ToDto(kudo);
	}

	public async Task<KudoFeedDto> GetFeedAsync(Guid? before, int limit, CancellationToken ct = default)
	{
		var cap = Math.Min(limit, 50);

		IQueryable<Kudo> query = Db.Kudos
			.Include(k => k.Recipients)
			.OrderByDescending(k => k.CreatedAt)
			.ThenByDescending(k => k.Id);

		if (before.HasValue)
		{
			var cursor = await Db.Kudos
				.AsNoTracking()
				.Where(k => k.Id == before.Value)
				.Select(k => new { k.CreatedAt, k.Id })
				.FirstOrDefaultAsync(ct);

			if (cursor is not null)
			{
				query = Db.Kudos
					.Include(k => k.Recipients)
					.Where(k => k.CreatedAt < cursor.CreatedAt
						|| (k.CreatedAt == cursor.CreatedAt && k.Id.CompareTo(cursor.Id) < 0))
					.OrderByDescending(k => k.CreatedAt)
					.ThenByDescending(k => k.Id);
			}
		}

		var items = await query.Take(cap + 1).ToListAsync(ct);
		var hasMore = items.Count > cap;
		if (hasMore) items = items.Take(cap).ToList();

		return new KudoFeedDto
		{
			Kudos = items.Select(ToDto).ToList(),
			HasMore = hasMore,
		};
	}

	public async Task<IReadOnlyList<KudoDto>> GetReceivedAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var kudoIds = await Db.KudoRecipients
			.AsNoTracking()
			.Where(r => r.CollaboratorId == collaboratorId)
			.Select(r => r.KudoId)
			.ToListAsync(ct);

		var kudos = await Db.Kudos
			.AsNoTracking()
			.Include(k => k.Recipients)
			.Where(k => kudoIds.Contains(k.Id))
			.OrderByDescending(k => k.CreatedAt)
			.ToListAsync(ct);

		return kudos.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<KudoDto>> GetGivenAsync(Guid giverId, CancellationToken ct = default)
	{
		var kudos = await Db.Kudos
			.AsNoTracking()
			.Include(k => k.Recipients)
			.Where(k => k.GiverId == giverId)
			.OrderByDescending(k => k.CreatedAt)
			.ToListAsync(ct);

		return kudos.Select(ToDto).ToList();
	}

	private static KudoDto ToDto(Kudo k) => new()
	{
		Id = k.Id,
		GiverId = k.GiverId,
		GiverName = k.GiverName,
		GiverPhotoUrl = k.GiverPhotoUrl,
		Value = k.Value,
		Message = k.Message,
		CreatedAtUtc = k.CreatedAt,
		Recipients = k.Recipients.Select(r => new KudoRecipientDto
		{
			CollaboratorId = r.CollaboratorId,
			CollaboratorName = r.CollaboratorName,
			CollaboratorPhotoUrl = r.CollaboratorPhotoUrl,
		}).ToList(),
	};
}
