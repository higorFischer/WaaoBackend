using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Focus;

namespace Waao.Infra.EF.Configurations;

public class WeeklyFocusConfiguration : IEntityTypeConfiguration<WeeklyFocus>
{
	public void Configure(EntityTypeBuilder<WeeklyFocus> builder)
	{
		builder.ToTable("weekly_focuses");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(2000);
		builder.Property(x => x.OwnerName).IsRequired().HasMaxLength(200);

		builder.HasOne(x => x.Owner)
			.WithMany()
			.HasForeignKey(x => x.OwnerId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(x => x.Goals)
			.WithOne(g => g.WeeklyFocus)
			.HasForeignKey(g => g.WeeklyFocusId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.Projects)
			.WithOne(p => p.WeeklyFocus)
			.HasForeignKey(p => p.WeeklyFocusId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.IsoYear, x.IsoWeek });
		builder.HasIndex(x => new { x.IsPublished, x.StartDate, x.EndDate });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class WeeklyFocusGoalConfiguration : IEntityTypeConfiguration<WeeklyFocusGoal>
{
	public void Configure(EntityTypeBuilder<WeeklyFocusGoal> builder)
	{
		builder.ToTable("weekly_focus_goals");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.Text).IsRequired().HasMaxLength(500);

		builder.HasIndex(x => x.WeeklyFocusId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class WeeklyFocusProjectConfiguration : IEntityTypeConfiguration<WeeklyFocusProject>
{
	public void Configure(EntityTypeBuilder<WeeklyFocusProject> builder)
	{
		builder.ToTable("weekly_focus_projects");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.ProjectTitle).IsRequired().HasMaxLength(200);
		builder.Property(x => x.ProjectColorHex).IsRequired().HasMaxLength(16);

		builder.HasOne(x => x.Project)
			.WithMany()
			.HasForeignKey(x => x.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.WeeklyFocusId, x.ProjectId }).IsUnique();

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
