using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Design;

public class DesignAsset : Entity
{
	public Guid StepId { get; set; }
	public virtual DesignStep Step { get; set; } = null!;

	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public DesignAssetKind Kind { get; set; } = DesignAssetKind.Other;

	public string Url { get; set; } = string.Empty;
	public string R2Key { get; set; } = string.Empty;
	public long SizeBytes { get; set; }

	public bool ShowFullByDefault { get; set; }

	public Guid UploadedById { get; set; }
}
