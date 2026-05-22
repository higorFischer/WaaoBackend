using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Messaging;

public class Channel : Entity
{
	public string? Name { get; set; }
	public string? Description { get; set; }
	public ChannelKind Kind { get; set; } = ChannelKind.Public;
	public ChannelScope Scope { get; set; } = ChannelScope.Custom;
	public Guid? DepartmentId { get; set; }
	public virtual Department? Department { get; set; }
	public Guid CreatedById { get; set; }
	public virtual Collaborator CreatedBy { get; set; } = null!;

	public virtual ICollection<ChannelMember> Members { get; set; } = [];
	public virtual ICollection<Message> Messages { get; set; } = [];
}
