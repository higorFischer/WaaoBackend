using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Design;

namespace Waao.Infra.EF.Configurations;

public class DesignFlowConfiguration : IEntityTypeConfiguration<DesignFlow>
{
	public void Configure(EntityTypeBuilder<DesignFlow> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Description).HasMaxLength(2000);
		builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

		builder.HasIndex(x => new { x.Status, x.IsDeleted });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class DesignStepConfiguration : IEntityTypeConfiguration<DesignStep>
{
	public void Configure(EntityTypeBuilder<DesignStep> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Description).HasMaxLength(2000);
		builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

		builder.HasOne(x => x.Flow)
			.WithMany(f => f.Steps)
			.HasForeignKey(x => x.FlowId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.FlowId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class DesignStepEdgeConfiguration : IEntityTypeConfiguration<DesignStepEdge>
{
	public void Configure(EntityTypeBuilder<DesignStepEdge> builder)
	{
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Flow)
			.WithMany(f => f.Edges)
			.HasForeignKey(x => x.FlowId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.SourceStep)
			.WithMany()
			.HasForeignKey(x => x.SourceStepId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.TargetStep)
			.WithMany()
			.HasForeignKey(x => x.TargetStepId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.FlowId);
		builder.HasIndex(x => new { x.FlowId, x.SourceStepId, x.TargetStepId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class DesignAssetConfiguration : IEntityTypeConfiguration<DesignAsset>
{
	public void Configure(EntityTypeBuilder<DesignAsset> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.FileName).IsRequired().HasMaxLength(160);
		builder.Property(x => x.ContentType).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
		builder.Property(x => x.Url).IsRequired().HasMaxLength(1000);
		builder.Property(x => x.R2Key).IsRequired().HasMaxLength(1000);

		builder.HasOne(x => x.Step)
			.WithMany(s => s.Assets)
			.HasForeignKey(x => x.StepId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.StepId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
