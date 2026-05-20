using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities;

namespace Waao.Infra.EF.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
	public void Configure(EntityTypeBuilder<Course> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
		builder.Property(x => x.Provider).HasMaxLength(120);
		builder.Property(x => x.MaterialUrl).HasMaxLength(500);
		builder.Property(x => x.Category).IsRequired().HasMaxLength(80);
		builder.Property(x => x.IsPublished).HasDefaultValue(false);

		builder.HasOne(x => x.CreatedBy)
			.WithMany()
			.HasForeignKey(x => x.CreatedById)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.IsPublished, x.IsDeleted });
		builder.HasIndex(x => x.Category);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CourseCompletionConfiguration : IEntityTypeConfiguration<CourseCompletion>
{
	public void Configure(EntityTypeBuilder<CourseCompletion> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Notes).HasMaxLength(500);

		builder.HasOne(x => x.Course)
			.WithMany(c => c.Completions)
			.HasForeignKey(x => x.CourseId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CourseId, x.CollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => new { x.CollaboratorId, x.IsDeleted });
		builder.HasIndex(x => x.CourseId);

		builder.HasIndex(x => x.XpAwardedAt)
			.HasFilter("xp_awarded_at IS NULL AND is_deleted = false");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
