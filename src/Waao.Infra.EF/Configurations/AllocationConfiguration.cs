using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Allocation;

namespace Waao.Infra.EF.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.Property(x => x.ColorHex).IsRequired().HasMaxLength(9);
		builder.Property(x => x.IsArchived).HasDefaultValue(false);

		builder.HasIndex(x => new { x.IsArchived, x.IsDeleted });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ProjectAllocationConfiguration : IEntityTypeConfiguration<ProjectAllocation>
{
	public void Configure(EntityTypeBuilder<ProjectAllocation> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Note).HasMaxLength(500);

		builder.HasOne(x => x.Project)
			.WithMany(p => p.Allocations)
			.HasForeignKey(x => x.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.ProjectId, x.CollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.CollaboratorId);
		builder.HasIndex(x => x.ProjectId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
