using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Design;

public record DesignFlowDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public DesignFlowStatus Status { get; init; } = DesignFlowStatus.Active;
	public int StepCount { get; init; }
	public DateTime UpdatedAt { get; init; }
}

public record DesignAssetDto
{
	public Guid Id { get; init; }
	public Guid StepId { get; init; }
	public string FileName { get; init; } = string.Empty;
	public string ContentType { get; init; } = string.Empty;
	public DesignAssetKind Kind { get; init; } = DesignAssetKind.Other;
	public string Url { get; init; } = string.Empty;
	public long SizeBytes { get; init; }
	public bool ShowFullByDefault { get; init; }
	public Guid UploadedById { get; init; }
	public DateTime CreatedAt { get; init; }
}

public record DesignStepDto
{
	public Guid Id { get; init; }
	public Guid FlowId { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public DesignStepStatus Status { get; init; } = DesignStepStatus.NotStarted;
	public double PositionX { get; init; }
	public double PositionY { get; init; }
	public IReadOnlyList<DesignAssetDto> Assets { get; init; } = [];
}

public record DesignEdgeDto
{
	public Guid Id { get; init; }
	public Guid FlowId { get; init; }
	public Guid SourceStepId { get; init; }
	public Guid TargetStepId { get; init; }
}

public record DesignBoardDto
{
	public DesignFlowDto Flow { get; init; } = new();
	public IReadOnlyList<DesignStepDto> Steps { get; init; } = [];
	public IReadOnlyList<DesignEdgeDto> Edges { get; init; } = [];
}

public record CreateDesignFlowDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
}

public record UpdateDesignFlowDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public DesignFlowStatus Status { get; init; } = DesignFlowStatus.Active;
}

public record CreateDesignStepDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public double PositionX { get; init; }
	public double PositionY { get; init; }
}

public record UpdateDesignStepDto
{
	public string? Title { get; init; }
	public string? Description { get; init; }
	public DesignStepStatus? Status { get; init; }
	public double? PositionX { get; init; }
	public double? PositionY { get; init; }
}

public record CreateDesignEdgeDto
{
	public Guid SourceStepId { get; init; }
	public Guid TargetStepId { get; init; }
}

public record UpdateDesignAssetDto
{
	public bool ShowFullByDefault { get; init; }
}
