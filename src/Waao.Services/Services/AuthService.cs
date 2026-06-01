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
	ITenantContext TenantContext,
	ITenantService TenantService,
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

		// Route the registration by email domain. Tenants must allowlist a domain
		// before someone can self-register at it — that's the gating mechanism.
		// Falls back to WAAO when NO tenant has allowlisted any domain yet
		// (bootstrap path so the platform owner doesn't get locked out).
		var routedTenantId = await TenantService.ResolveTenantByEmailDomainAsync(emailLower, ct);
		var anyAllowlistConfigured = await Db.TenantAllowedEmailDomains.AsNoTracking().AnyAsync(ct);
		if (routedTenantId is null && anyAllowlistConfigured)
		{
			throw new FluentValidation.ValidationException(
				[new FluentValidation.Results.ValidationFailure("email", "Your email domain isn't enabled. Ask your workspace admin to allowlist it.")]);
		}
		var tenantId = routedTenantId ?? Tenancy.TenantConstants.WaaoTenantId;

		// Per-tenant uniqueness: same email CAN exist in another tenant, but not
		// twice in the same tenant. IgnoreQueryFilters because we're pre-auth.
		var duplicate = await Db.Collaborators.IgnoreQueryFilters()
			.AnyAsync(c => c.Email == emailLower && c.TenantId == tenantId && !c.IsDeleted, ct);
		if (duplicate)
			throw new FluentValidation.ValidationException(
				[new FluentValidation.Results.ValidationFailure("email", "Email is already in use.")]);

		var adminEmails = Configuration.GetSection("Auth:AdminEmails").Get<string[]>() ?? [];
		var isAdmin = adminEmails.Any(e => string.Equals(e, emailLower, StringComparison.OrdinalIgnoreCase));

		// Auto-verify when the registration was routed via an allowlisted domain —
		// the tenant has already vouched for everyone there, so the email
		// round-trip would just be ceremony.
		var trustedDomain = routedTenantId is not null;

		var entity = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			TenantId = tenantId,
			FullName = dto.FullName,
			Email = emailLower,
			JoinDate = dto.JoinDate,
			RoleKind = isAdmin ? Domain.Models.Enums.CollaboratorRoleKind.Admin : Domain.Models.Enums.CollaboratorRoleKind.Collaborator,
			PasswordHash = PasswordHasher.Hash(dto.Password),
			EmailVerified = trustedDomain,
			EmailVerifiedAt = trustedDomain ? DateTime.UtcNow : null,
			EmailVerificationToken = trustedDomain ? null : NewToken(),
			EmailVerificationTokenExpiresAt = trustedDomain ? null : DateTime.UtcNow.AddHours(24),
			LastVerificationEmailSentAt = trustedDomain ? null : DateTime.UtcNow,
		};
		Db.Collaborators.Add(entity);
		await Db.SaveChangesAsync(ct);

		if (trustedDomain)
		{
			Logger.LogInformation("Registered {Email} into tenant {Tenant} via allowlisted domain — skipped email verification.", emailLower, tenantId);
			return new RegisterResultDto { Status = "auto_verified", Email = entity.Email };
		}

		var baseUrl = Configuration["Frontend:BaseUrl"] ?? "https://waao-frontend.higorflopes.workers.dev";
		var verifyUrl = $"{baseUrl}/verify-email?token={entity.EmailVerificationToken}";
		try
		{
			await EmailSender.SendVerificationAsync(entity.Email, entity.FullName, verifyUrl, ct);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex)
		{
			// Diagnostic: surface the actual provider error so the operator can fix Resend domain
			// verification, API key, etc. — instead of "email_not_verified" forever.
			Logger.LogError(ex, "Verification email send failed for {Email}. verifyUrl={VerifyUrl}", entity.Email, verifyUrl);
		}

		return new RegisterResultDto { Status = "verification_sent", Email = entity.Email };
	}

	public async Task<AuthResultDto> VerifyEmailAsync(VerifyEmailDto dto, CancellationToken ct = default)
	{
		await VerifyEmailValidator.ValidateAndThrowAsync(dto, ct);

		// Pre-auth path: token lookup must cross tenants, then pin the tenant
		// context so SaveChanges/streak/badge writes target the right tenant.
		var c = await Db.Collaborators
			.IgnoreQueryFilters()
			.Where(x => !x.IsDeleted)
			.Include(x => x.Department).Include(x => x.Role).Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.EmailVerificationToken == dto.Token, ct);
		if (c is null || c.EmailVerificationTokenExpiresAt is null || c.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
			throw new InvalidVerificationTokenException();
		if (c.TenantId is { } tid) TenantContext.SetTenant(tid);

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
		// Pre-auth path — cross-tenant search, pin the tenant after.
		var c = await Db.Collaborators
			.IgnoreQueryFilters()
			.Where(x => !x.IsDeleted)
			.FirstOrDefaultAsync(x => x.Email == emailLower, ct);
		if (c is null || c.EmailVerified) return;
		if (c.TenantId is { } tid) TenantContext.SetTenant(tid);
		if (c.LastVerificationEmailSentAt is not null && (DateTime.UtcNow - c.LastVerificationEmailSentAt.Value).TotalSeconds < 60)
			return;

		c.EmailVerificationToken = NewToken();
		c.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
		c.LastVerificationEmailSentAt = DateTime.UtcNow;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var baseUrl = Configuration["Frontend:BaseUrl"] ?? "https://waao-frontend.higorflopes.workers.dev";
		var verifyUrl = $"{baseUrl}/verify-email?token={c.EmailVerificationToken}";
		try
		{
			await EmailSender.SendVerificationAsync(c.Email, c.FullName, verifyUrl, ct);
			Logger.LogInformation("Resent verification email to {Email} (token regenerated).", c.Email);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex)
		{
			// Don't swallow silently — re-throw so the controller returns a 500 and the user
			// sees an error instead of "we sent it!" while the email never goes out. The
			// caller (frontend) can render a generic "couldn't send right now, try again".
			Logger.LogError(ex, "Resend verification email failed for {Email}. verifyUrl={VerifyUrl}", c.Email, verifyUrl);
			throw new InvalidOperationException("Could not send verification email. Try again in a minute.", ex);
		}
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

	public async Task<CollaboratorDto> UpdateMyProfileAsync(Guid collaboratorId, UpdateMyProfileDto dto, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var name = (dto.FullName ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Full name is required.");
		if (name.Length > 160) name = name[..160];

		c.FullName = name;
		c.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		return CollaboratorMapper.ToDto(c);
	}

	public async Task<CollaboratorDto> UpdateMyPhotoAsync(Guid collaboratorId, string photoUrl, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		c.PhotoUrl = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		return CollaboratorMapper.ToDto(c);
	}

	public async Task<CollaboratorDto> SetDesktopNotificationsEnabledAsync(Guid collaboratorId, bool enabled, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		c.DesktopNotificationsEnabled = enabled;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		return CollaboratorMapper.ToDto(c);
	}

	/// <summary>
	/// Email lookup runs PRE-AUTH (no JWT yet) so the global tenant filter would
	/// silently scope it to the WAAO fallback and a Liberty user could never log
	/// in. We <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters"/>
	/// here, then pin the tenant context to the collaborator we found so any
	/// downstream writes inside the request scope (streak ticks, badge unlocks)
	/// land in the correct tenant.
	/// </summary>
	private async Task<Collaborator?> LoadByEmail(string email, CancellationToken ct)
	{
		var emailLower = email.Trim().ToLowerInvariant();
		var c = await Db.Collaborators
			.IgnoreQueryFilters()
			.Where(c => !c.IsDeleted)
			.Include(c => c.Department).Include(c => c.Role)
			.Include(c => c.Manager).Include(c => c.Badges)
			.FirstOrDefaultAsync(c => c.Email == emailLower, ct);
		if (c?.TenantId is { } tid) TenantContext.SetTenant(tid);
		return c;
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
