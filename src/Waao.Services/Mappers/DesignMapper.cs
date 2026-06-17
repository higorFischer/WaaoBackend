using Waao.Domain.Models.Entities.Design;
using Waao.Services.Abstractions.Dtos.Design;

namespace Waao.Services.Mappers;

public static class DesignMapper
{
	public static DesignFlowDto ToDto(DesignFlow f, int stepCount) => new()
	{
		Id = f.Id,
		Name = f.Name,
		Description = f.Description,
		Status = f.Status,
		StepCount = stepCount,
		UpdatedAt = f.UpdatedAt ?? f.CreatedAt,
	};

	public static DesignAssetDto ToDto(DesignAsset a) => new()
	{
		Id = a.Id,
		StepId = a.StepId,
		FileName = a.FileName,
		ContentType = a.ContentType,
		Kind = a.Kind,
		Url = a.Url,
		SizeBytes = a.SizeBytes,
		ShowFullByDefault = a.ShowFullByDefault,
		UploadedById = a.UploadedById,
		CreatedAt = a.CreatedAt,
	};

	public static DesignStepDto ToDto(DesignStep s) => new()
	{
		Id = s.Id,
		FlowId = s.FlowId,
		Title = s.Title,
		Description = s.Description,
		Status = s.Status,
		PositionX = s.PositionX,
		PositionY = s.PositionY,
		Assets = s.Assets
			.OrderBy(a => a.CreatedAt)
			.Select(ToDto)
			.ToList(),
	};

	public static DesignEdgeDto ToDto(DesignStepEdge e) => new()
	{
		Id = e.Id,
		FlowId = e.FlowId,
		SourceStepId = e.SourceStepId,
		TargetStepId = e.TargetStepId,
	};
}
