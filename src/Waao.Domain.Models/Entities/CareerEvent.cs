using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities;

public class CareerEvent : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public CareerEventType Type { get; set; }
	public DateOnly EventDate { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Notes { get; set; }

	public string? FromValue { get; set; }     // snapshot — e.g., previous role title
	public string? ToValue { get; set; }       // snapshot — e.g., new role title
	public decimal? XpAwarded { get; set; }
	public string? AttachmentUrl { get; set; }
}
