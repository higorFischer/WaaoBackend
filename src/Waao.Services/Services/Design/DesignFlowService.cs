using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.Design;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Design;
using Waao.Services.Abstractions.Services;
using Waao.Services.Mappers;

namespace Waao.Services.Services.Design;

public sealed class DesignFlowService(
	WaaoDbContext Db,
	IR2StorageService Storage,
	IValidator<CreateDesignFlowDto> CreateFlowValidator,
	IValidator<UpdateDesignFlowDto> UpdateFlowValidator,
	IValidator<CreateDesignStepDto> CreateStepValidator,
	IValidator<UpdateDesignStepDto> UpdateStepValidator,
	IValidator<CreateDesignEdgeDto> CreateEdgeValidator) : IDesignFlowService
{
	// ----- Flows -----

	public async Task<IReadOnlyList<DesignFlowDto>> GetFlowsAsync(CancellationToken ct = default)
		=> await Db.DesignFlows
			.AsNoTracking()
			.OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt)
			.Select(f => DesignMapper.ToDto(f, f.Steps.Count(s => !s.IsDeleted)))
			.ToListAsync(ct);

	public async Task<DesignFlowDto> CreateFlowAsync(CreateDesignFlowDto dto, CancellationToken ct = default)
	{
		await CreateFlowValidator.ValidateAndThrowAsync(dto, ct);

		var flow = new DesignFlow
		{
			Id = Guid.CreateVersion7(),
			Name = dto.Name.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			Status = DesignFlowStatus.Active,
		};
		Db.DesignFlows.Add(flow);
		await Db.SaveChangesAsync(ct);
		return DesignMapper.ToDto(flow, 0);
	}

	public async Task<DesignFlowDto> UpdateFlowAsync(Guid id, UpdateDesignFlowDto dto, CancellationToken ct = default)
	{
		await UpdateFlowValidator.ValidateAndThrowAsync(dto, ct);

		var flow = await Db.DesignFlows.FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"Design flow {id} not found.");

		flow.Name = dto.Name.Trim();
		flow.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		flow.Status = dto.Status;
		flow.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var stepCount = await Db.DesignSteps.CountAsync(s => s.FlowId == id, ct);
		return DesignMapper.ToDto(flow, stepCount);
	}

	public async Task DeleteFlowAsync(Guid id, CancellationToken ct = default)
	{
		var flow = await Db.DesignFlows.FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"Design flow {id} not found.");
		flow.IsDeleted = true;
		flow.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// ----- Board -----

	public async Task<DesignBoardDto> GetBoardAsync(Guid flowId, CancellationToken ct = default)
	{
		var flow = await Db.DesignFlows.AsNoTracking().FirstOrDefaultAsync(f => f.Id == flowId, ct)
			?? throw new KeyNotFoundException($"Design flow {flowId} not found.");

		var steps = await Db.DesignSteps
			.AsNoTracking()
			.Where(s => s.FlowId == flowId)
			.Include(s => s.Assets)
			.OrderBy(s => s.CreatedAt)
			.ToListAsync(ct);

		var edges = await Db.DesignStepEdges
			.AsNoTracking()
			.Where(e => e.FlowId == flowId)
			.ToListAsync(ct);

		return new DesignBoardDto
		{
			Flow = DesignMapper.ToDto(flow, steps.Count),
			Steps = steps.Select(DesignMapper.ToDto).ToList(),
			Edges = edges.Select(DesignMapper.ToDto).ToList(),
		};
	}

	// ----- Steps -----

	public async Task<DesignStepDto> CreateStepAsync(Guid flowId, CreateDesignStepDto dto, CancellationToken ct = default)
	{
		await CreateStepValidator.ValidateAndThrowAsync(dto, ct);

		var flow = await Db.DesignFlows.FirstOrDefaultAsync(f => f.Id == flowId, ct)
			?? throw new KeyNotFoundException($"Design flow {flowId} not found.");

		var step = new DesignStep
		{
			Id = Guid.CreateVersion7(),
			FlowId = flowId,
			Title = dto.Title.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			Status = DesignStepStatus.NotStarted,
			PositionX = dto.PositionX,
			PositionY = dto.PositionY,
		};
		Db.DesignSteps.Add(step);
		flow.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return DesignMapper.ToDto(step);
	}

	public async Task<DesignStepDto> UpdateStepAsync(Guid stepId, UpdateDesignStepDto dto, CancellationToken ct = default)
	{
		await UpdateStepValidator.ValidateAndThrowAsync(dto, ct);

		var step = await Db.DesignSteps
			.Include(s => s.Assets)
			.FirstOrDefaultAsync(s => s.Id == stepId, ct)
			?? throw new KeyNotFoundException($"Design step {stepId} not found.");

		if (dto.Title is not null) step.Title = dto.Title.Trim();
		if (dto.Description is not null) step.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		if (dto.Status is { } status) step.Status = status;
		if (dto.PositionX is { } x) step.PositionX = x;
		if (dto.PositionY is { } y) step.PositionY = y;
		step.UpdatedAt = DateTime.UtcNow;

		await TouchFlowAsync(step.FlowId, ct);
		await Db.SaveChangesAsync(ct);
		return DesignMapper.ToDto(step);
	}

	public async Task DeleteStepAsync(Guid stepId, CancellationToken ct = default)
	{
		var step = await Db.DesignSteps.FirstOrDefaultAsync(s => s.Id == stepId, ct)
			?? throw new KeyNotFoundException($"Design step {stepId} not found.");

		// Drop any edges touching this step so the board never dangles a connection.
		var edges = await Db.DesignStepEdges
			.Where(e => e.SourceStepId == stepId || e.TargetStepId == stepId)
			.ToListAsync(ct);
		var now = DateTime.UtcNow;
		foreach (var edge in edges)
		{
			edge.IsDeleted = true;
			edge.DeletedAt = now;
		}

		// Soft-delete the step's assets as well.
		var assets = await Db.DesignAssets.Where(a => a.StepId == stepId).ToListAsync(ct);
		foreach (var asset in assets)
		{
			asset.IsDeleted = true;
			asset.DeletedAt = now;
		}

		step.IsDeleted = true;
		step.DeletedAt = now;
		await TouchFlowAsync(step.FlowId, ct);
		await Db.SaveChangesAsync(ct);
	}

	// ----- Edges -----

	public async Task<DesignEdgeDto> CreateEdgeAsync(Guid flowId, CreateDesignEdgeDto dto, CancellationToken ct = default)
	{
		await CreateEdgeValidator.ValidateAndThrowAsync(dto, ct);

		var flow = await Db.DesignFlows.FirstOrDefaultAsync(f => f.Id == flowId, ct)
			?? throw new KeyNotFoundException($"Design flow {flowId} not found.");

		var steps = await Db.DesignSteps
			.Where(s => s.FlowId == flowId && (s.Id == dto.SourceStepId || s.Id == dto.TargetStepId))
			.Select(s => s.Id)
			.ToListAsync(ct);
		if (!steps.Contains(dto.SourceStepId))
			throw new KeyNotFoundException($"Step {dto.SourceStepId} not found in flow {flowId}.");
		if (!steps.Contains(dto.TargetStepId))
			throw new KeyNotFoundException($"Step {dto.TargetStepId} not found in flow {flowId}.");

		var existing = await Db.DesignStepEdges
			.FirstOrDefaultAsync(e => e.FlowId == flowId && e.SourceStepId == dto.SourceStepId && e.TargetStepId == dto.TargetStepId, ct);
		if (existing is not null)
			return DesignMapper.ToDto(existing);

		var edge = new DesignStepEdge
		{
			Id = Guid.CreateVersion7(),
			FlowId = flowId,
			SourceStepId = dto.SourceStepId,
			TargetStepId = dto.TargetStepId,
		};
		Db.DesignStepEdges.Add(edge);
		flow.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return DesignMapper.ToDto(edge);
	}

	public async Task DeleteEdgeAsync(Guid edgeId, CancellationToken ct = default)
	{
		var edge = await Db.DesignStepEdges.FirstOrDefaultAsync(e => e.Id == edgeId, ct)
			?? throw new KeyNotFoundException($"Design edge {edgeId} not found.");
		edge.IsDeleted = true;
		edge.DeletedAt = DateTime.UtcNow;
		await TouchFlowAsync(edge.FlowId, ct);
		await Db.SaveChangesAsync(ct);
	}

	// ----- Assets -----

	public async Task<DesignAssetDto> AddAssetAsync(Guid stepId, Stream content, string fileName, string contentType, long sizeBytes, Guid uploadedById, CancellationToken ct = default)
	{
		var step = await Db.DesignSteps.FirstOrDefaultAsync(s => s.Id == stepId, ct)
			?? throw new KeyNotFoundException($"Design step {stepId} not found.");

		if (!Storage.IsEnabled)
			throw new InvalidOperationException("File storage is not configured.");

		var safeName = SafeFileName(fileName);
		var ext = Path.GetExtension(safeName).TrimStart('.').ToLowerInvariant();
		var kind = InferKind(contentType, ext);
		var key = $"waao/design-assets/{step.FlowId:N}/{stepId:N}/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.CreateVersion7():N}-{safeName}";

		var url = await Storage.UploadAsync(key, content, contentType, ct);

		var asset = new DesignAsset
		{
			Id = Guid.CreateVersion7(),
			StepId = stepId,
			FileName = safeName,
			ContentType = contentType,
			Kind = kind,
			Url = url,
			R2Key = key,
			SizeBytes = sizeBytes,
			ShowFullByDefault = kind is DesignAssetKind.Pdf or DesignAssetKind.Image,
			UploadedById = uploadedById,
		};
		Db.DesignAssets.Add(asset);
		await TouchFlowAsync(step.FlowId, ct);
		await Db.SaveChangesAsync(ct);
		return DesignMapper.ToDto(asset);
	}

	public async Task<DesignAssetDto> UpdateAssetAsync(Guid assetId, UpdateDesignAssetDto dto, CancellationToken ct = default)
	{
		var asset = await Db.DesignAssets
			.Include(a => a.Step)
			.FirstOrDefaultAsync(a => a.Id == assetId, ct)
			?? throw new KeyNotFoundException($"Design asset {assetId} not found.");

		asset.ShowFullByDefault = dto.ShowFullByDefault;
		asset.UpdatedAt = DateTime.UtcNow;
		if (asset.Step is not null)
			await TouchFlowAsync(asset.Step.FlowId, ct);
		await Db.SaveChangesAsync(ct);
		return DesignMapper.ToDto(asset);
	}

	public async Task DeleteAssetAsync(Guid assetId, CancellationToken ct = default)
	{
		var asset = await Db.DesignAssets
			.Include(a => a.Step)
			.FirstOrDefaultAsync(a => a.Id == assetId, ct)
			?? throw new KeyNotFoundException($"Design asset {assetId} not found.");
		asset.IsDeleted = true;
		asset.DeletedAt = DateTime.UtcNow;
		if (asset.Step is not null)
			await TouchFlowAsync(asset.Step.FlowId, ct);
		await Db.SaveChangesAsync(ct);
	}

	// ----- Helpers -----

	private async Task TouchFlowAsync(Guid flowId, CancellationToken ct)
	{
		var flow = await Db.DesignFlows.FirstOrDefaultAsync(f => f.Id == flowId, ct);
		if (flow is not null)
			flow.UpdatedAt = DateTime.UtcNow;
	}

	public static DesignAssetKind InferKind(string contentType, string extension)
	{
		var mime = (contentType ?? string.Empty).ToLowerInvariant();
		var ext = (extension ?? string.Empty).ToLowerInvariant();

		if (mime == "application/pdf" || ext == "pdf")
			return DesignAssetKind.Pdf;

		if (mime == "image/svg+xml" || ext is "svg" or "ico")
			return DesignAssetKind.Icon;

		if (mime.StartsWith("image/") || ext is "png" or "jpg" or "jpeg" or "webp" or "gif" or "bmp" or "heic" or "avif")
			return DesignAssetKind.Image;

		return DesignAssetKind.Other;
	}

	private static string SafeFileName(string fileName)
	{
		var name = Path.GetFileName(fileName ?? string.Empty);
		if (string.IsNullOrWhiteSpace(name))
			return "file";
		var cleaned = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '-').ToArray());
		return cleaned.Length > 120 ? cleaned[^120..] : cleaned;
	}
}
