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

public record DocGraphNodeDto
{
	public string Id { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public string Folder { get; init; } = string.Empty;
	public int LinkCount { get; init; }
}

public record DocGraphEdgeDto
{
	public string Source { get; init; } = string.Empty;
	public string Target { get; init; } = string.Empty;
}

public record DocGraphDto
{
	public IReadOnlyList<DocGraphNodeDto> Nodes { get; init; } = [];
	public IReadOnlyList<DocGraphEdgeDto> Edges { get; init; } = [];
}
