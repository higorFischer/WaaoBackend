using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class OnboardingService(
	WaaoDbContext Db,
	IValidator<CompleteOnboardingDto> CompleteValidator,
	ILogger<OnboardingService> Logger) : IOnboardingService
{
	public async Task<OnboardingStatusDto> GetStatusAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");
		return Map(c);
	}

	public async Task<OnboardingStatusDto> CompleteAsync(Guid collaboratorId, CompleteOnboardingDto dto, CancellationToken ct = default)
	{
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		// Idempotent: already-onboarded collaborators get their current status back; no writes/overwrite.
		if (c.OnboardingCompletedAt is not null)
			return Map(c);

		await CompleteValidator.ValidateAndThrowAsync(dto, ct);
		if (!await Db.Departments.AnyAsync(d => d.Id == dto.DepartmentId, ct))
			throw new ValidationException(
				[new FluentValidation.Results.ValidationFailure(nameof(dto.DepartmentId), "Department not found.")]);

		c.PhotoUrl = dto.PhotoUrl;
		c.Bio = dto.Bio;
		c.Birthdate = dto.Birthdate;
		c.DepartmentId = dto.DepartmentId;
		c.OnboardingCompletedAt = DateTime.UtcNow;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("Collaborator {Id} completed onboarding.", c.Id);
		return Map(c);
	}

	private static OnboardingStatusDto Map(Waao.Domain.Models.Entities.Collaborator c) => new()
	{
		Completed = c.OnboardingCompletedAt is not null,
		CompletedAt = c.OnboardingCompletedAt,
		PhotoSet = !string.IsNullOrWhiteSpace(c.PhotoUrl),
		BioSet = !string.IsNullOrWhiteSpace(c.Bio),
		BirthdateSet = c.Birthdate is not null && c.Birthdate != default(DateOnly),
		DepartmentSet = c.DepartmentId is not null && c.DepartmentId != Guid.Empty,
	};
}
