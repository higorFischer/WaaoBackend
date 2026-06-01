using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Calls;

namespace Waao.Infra.EF.Configurations;

public class CallChannelConfiguration : IEntityTypeConfiguration<CallChannel>
{
	public void Configure(EntityTypeBuilder<CallChannel> builder)
	{
		builder.ToTable("call_channels");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.ColorHex).IsRequired().HasMaxLength(16);
		builder.Property(x => x.CreatedByName).IsRequired().HasMaxLength(200);

		builder.HasIndex(x => new { x.IsArchived, x.Position });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
