using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities;
using Waao.Infra.EF;
using Waao.Services.Abstractions;
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
	IValidator<ChangePasswordDto> ChangePasswordValidator,
	IEmailSender EmailSender,
	IValidator<VerifyEmailDto> VerifyEmailValidator,
	IValidator<ResendVerificationDto> ResendValidator,
	IConfiguration Configuration,
	ILogger<AuthService> Logger) : IAuthService
{
	public async Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
	{
		await LoginValidator.ValidateAndThrowAsync(dto, ct);

		var collaborator = await LoadByEmail(dto.Email, ct)
			?? throw new UnauthorizedAccessException("Invalid email or password.");

		if (string.IsNullOrEmpty(collaborator.PasswordHash) ||
			!PasswordHasher.Verify(dto.Password, collaborator.PasswordHash))
			throw new UnauthorizedAccessException("Invalid email or password.");

		if (!collaborator.EmailVerified)
			throw new EmailNotVerifiedException(collaborator.Email);

		var (streakDays, _, bonus) = await Streaks.RegisterLoginAsync(collaborator.Id, ct: ct);
		await Db.SaveChangesAsync(ct);

		var newBadges = await Badges.EvaluateAsync(collaborator.Id, ct);
		await Db.SaveChangesAsync(ct);

		return BuildResult(collaborator, streakDays, bonus, newBadges);
	}

	public async Task<RegisterResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
	{
		await RegisterValidator.ValidateAndThrowAsync(dto, ct);

		var emailLower = dto.Email.Trim().ToLowerInvariant();

		if (await Db.Collaborators.AnyAsync(c => c.Email == emailLower, ct))
			throw new FluentValidation.ValidationException(
				[new FluentValidation.Results.ValidationFailure("email", "Email is already in use.")]);

		var adminEmails = Configuration.GetSection("Auth:AdminEmails").Get<string[]>() ?? [];
		var isAdmin = adminEmails.Any(e => string.Equals(e, emailLower, StringComparison.OrdinalIgnoreCase));

		var entity = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = dto.FullName,
			Email = emailLower,
			JoinDate = dto.JoinDate,
			RoleKind = isAdmin ? Domain.Models.Enums.CollaboratorRoleKind.Admin : Domain.Models.Enums.CollaboratorRoleKind.Collaborator,
			PasswordHash = PasswordHasher.Hash(dto.Password),
			EmailVerified = false,
			EmailVerificationToken = NewToken(),
			EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24),
			LastVerificationEmailSentAt = DateTime.UtcNow,
		};
		Db.Collaborators.Add(entity);
		await Db.SaveChangesAsync(ct);

		var baseUrl = Configuration["Frontend:BaseUrl"] ?? "https://waao-frontend.higorflopes.workers.dev";
		var verifyUrl = $"{baseUrl}/verify-email?token={entity.EmailVerificationToken}";
		try { await EmailSender.SendVerificationAsync(entity.Email, entity.FullName, verifyUrl, ct); }
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { Logger.LogError(ex, "Verification email send failed for {Email}", entity.Email); }

		return new RegisterResultDto { Status = "verification_sent", Email = entity.Email };
	}

	public async Task<AuthResultDto> VerifyEmailAsync(VerifyEmailDto dto, CancellationToken ct = default)
	{
		await VerifyEmailValidator.ValidateAndThrowAsync(dto, ct);

		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role).Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.EmailVerificationToken == dto.Token, ct);
		if (c is null || c.EmailVerificationTokenExpiresAt is null || c.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
			throw new InvalidVerificationTokenException();

		c.EmailVerified = true;
		c.EmailVerifiedAt = DateTime.UtcNow;
		c.EmailVerificationToken = null;
		c.EmailVerificationTokenExpiresAt = null;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var (streakDays, _, bonus) = await Streaks.RegisterLoginAsync(c.Id, ct: ct);
		await Db.SaveChangesAsync(ct);
		var newBadges = await Badges.EvaluateAsync(c.Id, ct);
		await Db.SaveChangesAsync(ct);
		return BuildResult(c, streakDays, bonus, newBadges);
	}

	public async Task ResendVerificationAsync(ResendVerificationDto dto, CancellationToken ct = default)
	{
		await ResendValidator.ValidateAndThrowAsync(dto, ct);

		var emailLower = dto.Email.Trim().ToLowerInvariant();
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Email == emailLower, ct);
		if (c is null || c.EmailVerified) return;
		if (c.LastVerificationEmailSentAt is not null && (DateTime.UtcNow - c.LastVerificationEmailSentAt.Value).TotalSeconds < 60)
			return;

		c.EmailVerificationToken = NewToken();
		c.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
		c.LastVerificationEmailSentAt = DateTime.UtcNow;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var baseUrl = Configuration["Frontend:BaseUrl"] ?? "https://waao-frontend.higorflopes.workers.dev";
		var verifyUrl = $"{baseUrl}/verify-email?token={c.EmailVerificationToken}";
		try { await EmailSender.SendVerificationAsync(c.Email, c.FullName, verifyUrl, ct); }
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { Logger.LogError(ex, "Resend verification email failed for {Email}", c.Email); }
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

	public async Task<AuthResultDto> RefreshAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new UnauthorizedAccessException("Collaborator not found.");

		// Sliding session: mint a brand-new token with the current expiry window.
		// Streak/badges are NOT re-evaluated here — refresh is a token bump only,
		// not a fresh login.
		var (token, expires) = Jwt.Issue(c);
		return new AuthResultDto
		{
			Token = token,
			ExpiresAt = expires,
			Me = CollaboratorMapper.ToDto(c),
		};
	}

	public async Task<CollaboratorDto?> GetMeAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct);
		return c is null ? null : CollaboratorMapper.ToDto(c);
	}

	private async Task<Collaborator?> LoadByEmail(string email, CancellationToken ct)
	{
		var emailLower = email.Trim().ToLowerInvariant();
		return await Db.Collaborators
			.Include(c => c.Department).Include(c => c.Role)
			.Include(c => c.Manager).Include(c => c.Badges)
			.FirstOrDefaultAsync(c => c.Email == emailLower, ct);
	}

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

	private static string NewToken()
	{
		var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
	}
}
