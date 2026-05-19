using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;
using Waao.Services.Mappers;

namespace Waao.Services.Services;

public sealed class CareerEventService(
	WaaoDbContext Db,
	GamificationEngine Gamification,
	StreakTracker Streaks,
	BadgeEvaluator Badges,
	IValidator<CreateCareerEventDto> CreateValidator) : ICareerEventService
{
	public async Task<IReadOnlyList<CareerEventDto>> GetForCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default)
		=> await Db.CareerEvents
			.Where(e => e.CollaboratorId == collaboratorId)
			.OrderByDescending(e => e.EventDate)
			.Select(e => CareerEventMapper.ToDto(e))
			.ToListAsync(ct);

	public async Task<CareerEventCreatedDto> CreateAsync(CreateCareerEventDto dto, CancellationToken ct = default)
	{
		await CreateValidator.ValidateAndThrowAsync(dto, ct);

		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == dto.CollaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {dto.CollaboratorId} not found");
		var levelBefore = collaborator.CurrentLevel;

		var entity = new CareerEvent
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = dto.CollaboratorId,
			Type = dto.Type,
			EventDate = dto.EventDate,
			Title = dto.Title,
			Notes = dto.Notes,
			FromValue = dto.FromValue,
			ToValue = dto.ToValue,
			AttachmentUrl = dto.AttachmentUrl,
		};
		Db.CareerEvents.Add(entity);

		// 1. XP for the event itself
		var eventXp = await Gamification.AwardCareerEventXpAsync(entity, ct);
		// 2. Streak state + any streak-threshold bonus
		var (currentStreak, _, streakBonus) = await Streaks.RegisterActivityAsync(dto.CollaboratorId, dto.EventDate, ct);
		// 3. Persist so the badge evaluator reads this event + the updated streak state
		await Db.SaveChangesAsync(ct);
		// 4. Award any newly-eligible badges (grants XP + creates audit rows)
		var newBadges = await Badges.EvaluateAsync(dto.CollaboratorId, ct);
		await Db.SaveChangesAsync(ct);

		// reload so we read the updated level
		await Db.Entry(collaborator).ReloadAsync(ct);

		return new CareerEventCreatedDto
		{
			Event = CareerEventMapper.ToDto(entity),
			XpAwarded = eventXp,
			StreakBonusXp = streakBonus,
			CurrentStreakDays = currentStreak,
			LevelBefore = levelBefore,
			LevelAfter = collaborator.CurrentLevel,
			NewBadges = newBadges.Select(b => new BadgeDto
			{
				Id = b.Id,
				Code = b.Code,
				Name = b.Name,
				Description = b.Description,
				IconEmoji = b.IconEmoji,
				IconUrl = b.IconUrl,
				Category = b.Category,
				Rarity = b.Rarity,
				XpReward = b.XpReward,
				UnlockRule = b.UnlockRule,
			}).ToList(),
		};
	}

	public async Task DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var entity = await Db.CareerEvents.FirstOrDefaultAsync(e => e.Id == id, ct)
			?? throw new KeyNotFoundException($"CareerEvent {id} not found");
		entity.IsDeleted = true;
		entity.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
}
