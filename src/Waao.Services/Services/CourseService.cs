using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Courses;
using Waao.Services.Abstractions.Services;
using Waao.Services.Authorization;
using Waao.Services.Gamification;

namespace Waao.Services.Services;

public sealed class CourseService(
	WaaoDbContext Db,
	IValidator<CreateCourseDto> CreateValidator,
	IValidator<UpdateCourseDto> UpdateValidator) : ICourseService
{
	public async Task<IReadOnlyList<CourseDto>> ListAsync(CourseListFilterDto filter, bool isAdminOrHr, CancellationToken ct = default)
	{
		var query = Db.Courses.AsQueryable();

		if (!isAdminOrHr)
			query = query.Where(c => c.IsPublished);
		else if (filter.OnlyPublished.HasValue)
			query = query.Where(c => c.IsPublished == filter.OnlyPublished.Value);

		if (!string.IsNullOrWhiteSpace(filter.Category))
			query = query.Where(c => c.Category == filter.Category);

		return await query
			.OrderBy(c => c.Title)
			.Select(c => ToDto(c))
			.ToListAsync(ct);
	}

	public async Task<CourseDto> GetByIdAsync(Guid id, bool isAdminOrHr, CancellationToken ct = default)
	{
		var course = await Db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Course {id} not found.");

		if (!isAdminOrHr && !course.IsPublished)
			throw new KeyNotFoundException($"Course {id} not found.");

		return ToDto(course);
	}

	public async Task<CourseDto> CreateAsync(CreateCourseDto dto, Guid authorId, CancellationToken ct = default)
	{
		await CreateValidator.ValidateAndThrowAsync(dto, ct);

		var course = new Course
		{
			Id = Guid.CreateVersion7(),
			Title = dto.Title,
			Description = dto.Description,
			Provider = dto.Provider,
			MaterialUrl = dto.MaterialUrl,
			DurationMinutes = dto.DurationMinutes,
			SuggestedXp = dto.SuggestedXp,
			Category = dto.Category,
			IsPublished = false,
			CreatedById = authorId,
		};

		Db.Courses.Add(course);
		await Db.SaveChangesAsync(ct);
		return ToDto(course);
	}

	public async Task<CourseDto> UpdateAsync(Guid id, UpdateCourseDto dto, CancellationToken ct = default)
	{
		await UpdateValidator.ValidateAndThrowAsync(dto, ct);

		var course = await Db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Course {id} not found.");

		course.Title = dto.Title;
		course.Description = dto.Description;
		course.Provider = dto.Provider;
		course.MaterialUrl = dto.MaterialUrl;
		course.DurationMinutes = dto.DurationMinutes;
		course.SuggestedXp = dto.SuggestedXp;
		course.Category = dto.Category;
		course.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return ToDto(course);
	}

	public async Task DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var course = await Db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Course {id} not found.");

		course.IsDeleted = true;
		course.DeletedAt = DateTime.UtcNow;
		course.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<CourseDto> PublishAsync(Guid id, bool isPublished, CancellationToken ct = default)
	{
		var course = await Db.Courses.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Course {id} not found.");

		course.IsPublished = isPublished;
		course.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToDto(course);
	}

	private static CourseDto ToDto(Course c) => new()
	{
		Id = c.Id,
		Title = c.Title,
		Description = c.Description,
		Provider = c.Provider,
		MaterialUrl = c.MaterialUrl,
		DurationMinutes = c.DurationMinutes,
		SuggestedXp = c.SuggestedXp,
		Category = c.Category,
		IsPublished = c.IsPublished,
		CreatedById = c.CreatedById,
		CreatedAt = c.CreatedAt,
	};
}

public sealed class CourseCompletionService(
	WaaoDbContext Db,
	GamificationEngine Gamification,
	IValidator<MarkCourseCompleteDto> MarkValidator,
	IValidator<GrantCourseXpDto> GrantValidator) : ICourseCompletionService
{
	public async Task<CourseCompletionDto> MarkCompleteAsync(Guid courseId, Guid collaboratorId, MarkCourseCompleteDto dto, CancellationToken ct = default)
	{
		await MarkValidator.ValidateAndThrowAsync(dto, ct);

		var course = await Db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, ct)
			?? throw new KeyNotFoundException($"Course {courseId} not found.");

		if (!course.IsPublished)
			throw new KeyNotFoundException($"Course {courseId} not found.");

		// Idempotent: return existing if already completed
		var existing = await Db.CourseCompletions
			.FirstOrDefaultAsync(cc => cc.CourseId == courseId && cc.CollaboratorId == collaboratorId, ct);

		if (existing is not null)
			return await BuildCompletionDtoAsync(existing, course, collaboratorId, ct);

		var completion = new CourseCompletion
		{
			Id = Guid.CreateVersion7(),
			CourseId = courseId,
			CollaboratorId = collaboratorId,
			CompletedAt = DateTime.UtcNow,
			Notes = dto.Notes,
		};

		Db.CourseCompletions.Add(completion);
		await Db.SaveChangesAsync(ct);
		return await BuildCompletionDtoAsync(completion, course, collaboratorId, ct);
	}

	public async Task<IReadOnlyList<CourseCompletionDto>> ListPendingForReviewAsync(CancellationToken ct = default)
	{
		return await Db.CourseCompletions
			.Where(cc => cc.XpAwardedAt == null)
			.Include(cc => cc.Course)
			.Include(cc => cc.Collaborator)
			.OrderBy(cc => cc.CompletedAt)
			.Select(cc => new CourseCompletionDto
			{
				Id = cc.Id,
				CourseId = cc.CourseId,
				CourseTitle = cc.Course.Title,
				CourseCategory = cc.Course.Category,
				CourseSuggestedXp = cc.Course.SuggestedXp,
				CollaboratorId = cc.CollaboratorId,
				CollaboratorName = cc.Collaborator.FullName,
				CompletedAt = cc.CompletedAt,
				Notes = cc.Notes,
				XpAwarded = cc.XpAwarded,
				XpAwardedAt = cc.XpAwardedAt,
				XpAwardedByAdminId = cc.XpAwardedByAdminId,
			})
			.ToListAsync(ct);
	}

	public async Task<CourseCompletionDto> GrantXpForCompletionAsync(Guid completionId, GrantCourseXpDto dto, Guid adminId, CancellationToken ct = default)
	{
		await GrantValidator.ValidateAndThrowAsync(dto, ct);

		var completion = await Db.CourseCompletions
			.Include(cc => cc.Course)
			.Include(cc => cc.Collaborator)
			.FirstOrDefaultAsync(cc => cc.Id == completionId, ct)
			?? throw new KeyNotFoundException($"CourseCompletion {completionId} not found.");

		// Idempotent: return current if already granted
		if (completion.XpAwardedAt is not null)
			return MapCompletionDto(completion);

		// Higher-rank-only rule: the reviewer must outrank the recipient.
		await RankGuard.EnsureCanGrantXpToAsync(Db, adminId, completion.CollaboratorId, ct);

		await Gamification.RecordAsync(
			completion.CollaboratorId,
			dto.Amount,
			XpSource.Admin,
			$"Course completed: {completion.Course.Title} [Category: {completion.Course.Category}]",
			completionId,
			"CourseCompletion",
			ct);

		completion.XpAwarded = dto.Amount;
		completion.XpAwardedAt = DateTime.UtcNow;
		completion.XpAwardedByAdminId = adminId;
		completion.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return MapCompletionDto(completion);
	}

	public async Task<IReadOnlyList<CourseCompletionDto>> ListMyCompletionsAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		return await Db.CourseCompletions
			.Where(cc => cc.CollaboratorId == collaboratorId)
			.Include(cc => cc.Course)
			.Include(cc => cc.Collaborator)
			.OrderByDescending(cc => cc.CompletedAt)
			.Select(cc => MapCompletionDto(cc))
			.ToListAsync(ct);
	}

	private async Task<CourseCompletionDto> BuildCompletionDtoAsync(CourseCompletion completion, Course course, Guid collaboratorId, CancellationToken ct)
	{
		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct);
		return new CourseCompletionDto
		{
			Id = completion.Id,
			CourseId = course.Id,
			CourseTitle = course.Title,
			CourseCategory = course.Category,
			CourseSuggestedXp = course.SuggestedXp,
			CollaboratorId = collaboratorId,
			CollaboratorName = collaborator?.FullName ?? string.Empty,
			CompletedAt = completion.CompletedAt,
			Notes = completion.Notes,
			XpAwarded = completion.XpAwarded,
			XpAwardedAt = completion.XpAwardedAt,
			XpAwardedByAdminId = completion.XpAwardedByAdminId,
		};
	}

	private static CourseCompletionDto MapCompletionDto(CourseCompletion cc) => new()
	{
		Id = cc.Id,
		CourseId = cc.CourseId,
		CourseTitle = cc.Course?.Title ?? string.Empty,
		CourseCategory = cc.Course?.Category ?? string.Empty,
		CourseSuggestedXp = cc.Course?.SuggestedXp,
		CollaboratorId = cc.CollaboratorId,
		CollaboratorName = cc.Collaborator?.FullName ?? string.Empty,
		CompletedAt = cc.CompletedAt,
		Notes = cc.Notes,
		XpAwarded = cc.XpAwarded,
		XpAwardedAt = cc.XpAwardedAt,
		XpAwardedByAdminId = cc.XpAwardedByAdminId,
	};
}
