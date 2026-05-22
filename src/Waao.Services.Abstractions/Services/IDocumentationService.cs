using Waao.Services.Abstractions.Dtos.Documentation;

namespace Waao.Services.Abstractions.Services;

public interface IDocumentationService
{
	Task<DocTreeNodeDto> GetTreeAsync(CancellationToken ct = default);
	Task<DocFileDto?> GetFileAsync(string relativePath, CancellationToken ct = default);
	Task<IReadOnlyList<DocSearchHitDto>> SearchAsync(string query, int maxResults = 50, CancellationToken ct = default);
	Task<DocRefreshResultDto> RefreshAsync(CancellationToken ct = default);
	Task<DocGraphDto> GetGraphAsync(CancellationToken ct = default);
}
