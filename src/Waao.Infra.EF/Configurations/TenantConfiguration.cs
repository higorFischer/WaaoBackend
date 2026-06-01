using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities;

namespace Waao.Infra.EF.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
	public void Configure(EntityTypeBuilder<Tenant> builder)
	{
		builder.ToTable("tenants");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Slug).IsRequired().HasMaxLength(40);
		builder.Property(x => x.LogoUrl).HasMaxLength(1000);
		builder.Property(x => x.AccentColorHex).IsRequired().HasMaxLength(16);

		builder.HasIndex(x => x.Slug).IsUnique().HasFilter("is_deleted = false");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
