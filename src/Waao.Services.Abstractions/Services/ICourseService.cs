using Waao.Services.Abstractions.Dtos.Courses;

namespace Waao.Services.Abstractions.Services;

public interface ICourseService
{
	Task<IReadOnlyList<CourseDto>> ListAsync(CourseListFilterDto filter, bool isAdminOrHr, CancellationToken ct = default);
	Task<CourseDto> GetByIdAsync(Guid id, bool isAdminOrHr, CancellationToken ct = default);
	Task<CourseDto> CreateAsync(CreateCourseDto dto, Guid authorId, CancellationToken ct = default);
	Task<CourseDto> UpdateAsync(Guid id, UpdateCourseDto dto, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);
	Task<CourseDto> PublishAsync(Guid id, bool isPublished, CancellationToken ct = default);
}

public interface ICourseCompletionService
{
	Task<CourseCompletionDto> MarkCompleteAsync(Guid courseId, Guid collaboratorId, MarkCourseCompleteDto dto, CancellationToken ct = default);
	Task<IReadOnlyList<CourseCompletionDto>> ListPendingForReviewAsync(CancellationToken ct = default);
	Task<CourseCompletionDto> GrantXpForCompletionAsync(Guid completionId, GrantCourseXpDto dto, Guid adminId, CancellationToken ct = default);
	Task<IReadOnlyList<CourseCompletionDto>> ListMyCompletionsAsync(Guid collaboratorId, CancellationToken ct = default);
}
