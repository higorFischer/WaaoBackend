using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Skills;
using Waao.Domain.Models.Entities.Team;

namespace Waao.Infra.EF.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
	public void Configure(EntityTypeBuilder<Skill> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Category).HasMaxLength(80);
		builder.Property(x => x.IsArchived).HasDefaultValue(false);

		builder.HasOne(x => x.Tenant)
			.WithMany()
			.HasForeignKey(x => x.TenantId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.IsArchived, x.IsDeleted });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CollaboratorSkillConfiguration : IEntityTypeConfiguration<CollaboratorSkill>
{
	public void Configure(EntityTypeBuilder<CollaboratorSkill> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Level).HasConversion<string>().HasMaxLength(20);
		builder.Property(x => x.Note).HasMaxLength(1000);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Skill)
			.WithMany(s => s.CollaboratorSkills)
			.HasForeignKey(x => x.SkillId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CollaboratorId, x.SkillId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.CollaboratorId);
		builder.HasIndex(x => x.SkillId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ManagerNoteConfiguration : IEntityTypeConfiguration<ManagerNote>
{
	public void Configure(EntityTypeBuilder<ManagerNote> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Body).IsRequired().HasMaxLength(8000);
		builder.Property(x => x.Pinned).HasDefaultValue(false);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CollaboratorId, x.Pinned });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
