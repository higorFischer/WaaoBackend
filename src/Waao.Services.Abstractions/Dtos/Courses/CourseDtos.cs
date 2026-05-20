namespace Waao.Services.Abstractions.Dtos.Courses;

public record CourseDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string? Provider { get; init; }
	public string? MaterialUrl { get; init; }
	public int? DurationMinutes { get; init; }
	public int? SuggestedXp { get; init; }
	public string Category { get; init; } = string.Empty;
	public bool IsPublished { get; init; }
	public Guid CreatedById { get; init; }
	public DateTime CreatedAt { get; init; }
}

public record CreateCourseDto
{
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string? Provider { get; init; }
	public string? MaterialUrl { get; init; }
	public int? DurationMinutes { get; init; }
	public int? SuggestedXp { get; init; }
	public string Category { get; init; } = string.Empty;
}

public record UpdateCourseDto
{
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string? Provider { get; init; }
	public string? MaterialUrl { get; init; }
	public int? DurationMinutes { get; init; }
	public int? SuggestedXp { get; init; }
	public string Category { get; init; } = string.Empty;
}

public record CourseListFilterDto
{
	public string? Category { get; init; }
	public bool? OnlyPublished { get; init; }
}

public record CourseCompletionDto
{
	public Guid Id { get; init; }
	public Guid CourseId { get; init; }
	public string CourseTitle { get; init; } = string.Empty;
	public string CourseCategory { get; init; } = string.Empty;
	public int? CourseSuggestedXp { get; init; }
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public DateTime CompletedAt { get; init; }
	public string? Notes { get; init; }
	public int? XpAwarded { get; init; }
	public DateTime? XpAwardedAt { get; init; }
	public Guid? XpAwardedByAdminId { get; init; }
}

public record MarkCourseCompleteDto
{
	public string? Notes { get; init; }
}

public record GrantCourseXpDto
{
	public int Amount { get; init; }
}

public record PublishCourseDto
{
	public bool IsPublished { get; init; }
}
