using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Announcements;

namespace Waao.Infra.EF.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
	public void Configure(EntityTypeBuilder<Announcement> builder)
	{
		builder.ToTable("announcements");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Body).HasMaxLength(2000);
		builder.Property(x => x.ImageUrl).HasMaxLength(1000);
		builder.Property(x => x.LogoUrl).HasMaxLength(1000);
		builder.Property(x => x.CountdownLabel).HasMaxLength(120);
		builder.Property(x => x.AccentColorHex).IsRequired().HasMaxLength(16);
		builder.Property(x => x.CreatedByName).IsRequired().HasMaxLength(200);

		builder.Property(x => x.Audience).HasConversion<string>().HasMaxLength(20);
		builder.Property(x => x.RecurrenceKind).HasConversion<string>().HasMaxLength(20);
		builder.Property(x => x.Effect).HasConversion<string>().HasMaxLength(20);
		builder.Property(x => x.TargetRoleKind).HasConversion<string>().HasMaxLength(20);

		builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
		builder.HasMany(x => x.Targets).WithOne(t => t.Announcement).HasForeignKey(t => t.AnnouncementId).OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.IsArchived, x.StartsAtUtc, x.EndsAtUtc });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class AnnouncementTargetConfiguration : IEntityTypeConfiguration<AnnouncementTarget>
{
	public void Configure(EntityTypeBuilder<AnnouncementTarget> builder)
	{
		builder.ToTable("announcement_targets");
		builder.HasKey(x => x.Id);
		builder.HasOne(x => x.Collaborator).WithMany().HasForeignKey(x => x.CollaboratorId).OnDelete(DeleteBehavior.Cascade);
		builder.HasIndex(x => new { x.AnnouncementId, x.CollaboratorId }).IsUnique();
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
