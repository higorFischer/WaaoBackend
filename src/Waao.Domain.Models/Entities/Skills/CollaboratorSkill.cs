using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Skills;

public class CollaboratorSkill : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator? Collaborator { get; set; }

	public Guid SkillId { get; set; }
	public virtual Skill? Skill { get; set; }

	public SkillLevel Level { get; set; } = SkillLevel.Competent;
	public string? Note { get; set; }

	public Guid AssessedById { get; set; }
	public DateTime AssessedAt { get; set; }
}
