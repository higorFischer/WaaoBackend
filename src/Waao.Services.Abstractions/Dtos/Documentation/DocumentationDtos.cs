namespace Waao.Services.Abstractions.Dtos.Documentation;

public record DocTreeNodeDto
{
	public string Name { get; init; } = string.Empty;
	public string Path { get; init; } = string.Empty;
	public bool IsFolder { get; init; }
	public IReadOnlyList<DocTreeNodeDto> Children { get; init; } = [];
}

public record DocFileDto
{
	public string Path { get; init; } = string.Empty;
	public string Content { get; init; } = string.Empty;
	public IReadOnlyDictionary<string, string> Frontmatter { get; init; } = new Dictionary<string, string>();
	public DateTime LastModifiedUtc { get; init; }
	public long SizeBytes { get; init; }
}

public record DocSearchHitDto
{
	public string Path { get; init; } = string.Empty;
	public int LineNumber { get; init; }
	public string Snippet { get; init; } = string.Empty;
}

public record DocRefreshResultDto
{
	public string Status { get; init; } = string.Empty;
	public string CommitSha { get; init; } = string.Empty;
	public DateTime PulledAtUtc { get; init; }
	public int FileCount { get; init; }
}
