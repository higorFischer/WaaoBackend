using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.OneOnOnes;

namespace Waao.Infra.EF.Configurations;

public class OneOnOneConfiguration : IEntityTypeConfiguration<OneOnOne>
{
	public void Configure(EntityTypeBuilder<OneOnOne> builder)
	{
		builder.ToTable("one_on_ones");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.ManagerName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.ReportName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Agenda).HasMaxLength(8000);
		builder.Property(x => x.Notes).HasMaxLength(16000);
		builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

		builder.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
		builder.HasMany(x => x.ActionItems).WithOne(i => i.OneOnOne).HasForeignKey(i => i.OneOnOneId).OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.ManagerId, x.ScheduledDate });
		builder.HasIndex(x => new { x.ReportId, x.ScheduledDate });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class OneOnOneActionItemConfiguration : IEntityTypeConfiguration<OneOnOneActionItem>
{
	public void Configure(EntityTypeBuilder<OneOnOneActionItem> builder)
	{
		builder.ToTable("one_on_one_action_items");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Text).IsRequired().HasMaxLength(1000);
		builder.Property(x => x.AssignedToName).HasMaxLength(200);

		builder.HasIndex(x => new { x.AssignedToId, x.IsDone });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
