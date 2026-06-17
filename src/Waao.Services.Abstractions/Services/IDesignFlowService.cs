using Waao.Services.Abstractions.Dtos.Design;

namespace Waao.Services.Abstractions.Services;

/// <summary>
/// Design Flow board management — freeform react-flow pipelines tracking a product's
/// visual-identity / design launch. Tenant-scoped; readable and editable by any
/// authenticated collaborator.
/// </summary>
public interface IDesignFlowService
{
	// ----- Flows -----
	Task<IReadOnlyList<DesignFlowDto>> GetFlowsAsync(CancellationToken ct = default);
	Task<DesignFlowDto> CreateFlowAsync(CreateDesignFlowDto dto, CancellationToken ct = default);
	Task<DesignFlowDto> UpdateFlowAsync(Guid id, UpdateDesignFlowDto dto, CancellationToken ct = default);
	Task DeleteFlowAsync(Guid id, CancellationToken ct = default);

	// ----- Board -----
	Task<DesignBoardDto> GetBoardAsync(Guid flowId, CancellationToken ct = default);

	// ----- Steps -----
	Task<DesignStepDto> CreateStepAsync(Guid flowId, CreateDesignStepDto dto, CancellationToken ct = default);
	Task<DesignStepDto> UpdateStepAsync(Guid stepId, UpdateDesignStepDto dto, CancellationToken ct = default);
	Task DeleteStepAsync(Guid stepId, CancellationToken ct = default);

	// ----- Edges -----
	Task<DesignEdgeDto> CreateEdgeAsync(Guid flowId, CreateDesignEdgeDto dto, CancellationToken ct = default);
	Task DeleteEdgeAsync(Guid edgeId, CancellationToken ct = default);

	// ----- Assets -----
	Task<DesignAssetDto> AddAssetAsync(Guid stepId, Stream content, string fileName, string contentType, long sizeBytes, Guid uploadedById, CancellationToken ct = default);
	Task<DesignAssetDto> UpdateAssetAsync(Guid assetId, UpdateDesignAssetDto dto, CancellationToken ct = default);
	Task DeleteAssetAsync(Guid assetId, CancellationToken ct = default);
}
