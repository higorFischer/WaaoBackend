namespace Waao.Domain.Models.Entities.Skills;

public class Skill : Entity
{
	public Guid? TenantId { get; set; }
	public virtual Tenant? Tenant { get; set; }

	public string Name { get; set; } = string.Empty;
	public string? Category { get; set; }
	public bool IsArchived { get; set; }

	public virtual ICollection<CollaboratorSkill> CollaboratorSkills { get; set; } = [];
}
