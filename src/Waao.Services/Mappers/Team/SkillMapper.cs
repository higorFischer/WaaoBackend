using Waao.Domain.Models.Entities.Skills;
using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Mappers.Team;

public static class SkillMapper
{
	public static SkillDto ToDto(Skill s) => new()
	{
		Id = s.Id,
		Name = s.Name,
		Category = s.Category,
		IsArchived = s.IsArchived,
	};

	public static CollaboratorSkillDto ToDto(CollaboratorSkill cs) => new()
	{
		Id = cs.Id,
		CollaboratorId = cs.CollaboratorId,
		SkillId = cs.SkillId,
		SkillName = cs.Skill != null ? cs.Skill.Name : string.Empty,
		SkillCategory = cs.Skill != null ? cs.Skill.Category : null,
		Level = cs.Level,
		Note = cs.Note,
		AssessedById = cs.AssessedById,
		AssessedAt = cs.AssessedAt,
	};
}
