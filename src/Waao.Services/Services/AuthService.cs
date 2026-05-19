using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Auth;
using Waao.Services.Gamification;
using Waao.Services.Mappers;

namespace Waao.Services.Services;

public sealed class AuthService(
	WaaoDbContext Db,
	JwtIssuer Jwt,
	StreakTracker Streaks,
	BadgeEvaluator Badges,
	IValidator<RegisterDto> RegisterValidator,
	IValidator<LoginDto> LoginValidator,
	IValidator<ChangePasswordDto> ChangePasswordValidator) : IAuthService
{
	public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
	{
		await LoginValidator.ValidateAndThrowAsync(dto, ct);

		var collaborator = await LoadByEmail(dto.Email, ct)
			?? throw new UnauthorizedAccessException("Invalid email or password.");

		if (string.IsNullOrEmpty(collaborator.PasswordHash) ||
			!PasswordHasher.Verify(dto.Password, collaborator.PasswordHash))
			throw new UnauthorizedAccessException("Invalid email or password.");

		var (streakDays, _, bonus) = await Streaks.RegisterLoginAsync(collaborator.Id, ct: ct);
		await Db.SaveChangesAsync(ct);

		var newBadges = await Badges.EvaluateAsync(collaborator.Id, ct);
		await Db.SaveChangesAsync(ct);

		return BuildResult(collaborator, streakDays, bonus, newBadges);
	}

	public async Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
	{
		await RegisterValidator.ValidateAndThrowAsync(dto, ct);

		var emailExists = await Db.Collaborators.AnyAsync(c => c.Email == dto.Email, ct);
		if (emailExists)
			throw new ValidationException(
				[new FluentValidation.Results.ValidationFailure("email", "Email is already in use.")]);

		var entity = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = dto.FullName,
			Email = dto.Email,
			JoinDate = dto.JoinDate,
			DepartmentId = dto.DepartmentId,
			RoleId = dto.RoleId,
			PasswordHash = PasswordHasher.Hash(dto.Password),
		};
		Db.Collaborators.Add(entity);
		await Db.SaveChangesAsync(ct);

		// Day-one badges (WELCOME, tenure-at-hire) + login streak start
		var firstPass = await Badges.EvaluateAsync(entity.Id, ct);
		var (streakDays, _, bonus) = await Streaks.RegisterLoginAsync(entity.Id, ct: ct);
		await Db.SaveChangesAsync(ct);
		var secondPass = await Badges.EvaluateAsync(entity.Id, ct);
		await Db.SaveChangesAsync(ct);

		var newBadges = firstPass.Concat(secondPass).ToList();
		return BuildResult(entity, streakDays, bonus, newBadges);
	}

	public async Task ChangePasswordAsync(Guid collaboratorId, ChangePasswordDto dto, CancellationToken ct = default)
	{
		await ChangePasswordValidator.ValidateAndThrowAsync(dto, ct);

		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		if (string.IsNullOrEmpty(c.PasswordHash) || !PasswordHasher.Verify(dto.CurrentPassword, c.PasswordHash))
			throw new UnauthorizedAccessException("Current password is incorrect.");

		c.PasswordHash = PasswordHasher.Hash(dto.NewPassword);
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<CollaboratorDto?> GetMeAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct);
		return c is null ? null : CollaboratorMapper.ToDto(c);
	}

	private async Task<Collaborator?> LoadByEmail(string email, CancellationToken ct) =>
		await Db.Collaborators
			.Include(c => c.Department).Include(c => c.Role)
			.Include(c => c.Manager).Include(c => c.Badges)
			.FirstOrDefaultAsync(c => c.Email == email, ct);

	private AuthResultDto BuildResult(
		Collaborator entity,
		int loginStreakDays,
		int loginBonusXp,
		IReadOnlyList<Badge> newBadges)
	{
		var (token, expires) = Jwt.Issue(entity);
		return new AuthResultDto
		{
			Token = token,
			ExpiresAt = expires,
			Me = CollaboratorMapper.ToDto(entity),
			LoginStreakDays = loginStreakDays,
			LoginStreakBonusXp = loginBonusXp,
			NewBadges = newBadges.Select(b => new BadgeDto
			{
				Id = b.Id, Code = b.Code, Name = b.Name, Description = b.Description,
				IconEmoji = b.IconEmoji, IconUrl = b.IconUrl,
				Category = b.Category, Rarity = b.Rarity,
				XpReward = b.XpReward, UnlockRule = b.UnlockRule,
			}).ToList(),
		};
	}
}
