using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities;

namespace Waao.Infra.EF.Configurations;

public class TenantAllowedEmailDomainConfiguration : IEntityTypeConfiguration<TenantAllowedEmailDomain>
{
	public void Configure(EntityTypeBuilder<TenantAllowedEmailDomain> builder)
	{
		builder.ToTable("tenant_allowed_email_domains");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Domain).IsRequired().HasMaxLength(253);

		builder.HasOne(x => x.Tenant)
			.WithMany()
			.HasForeignKey(x => x.TenantId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.TenantId, x.Domain })
			.IsUnique()
			.HasFilter("is_deleted = false");

		// No global tenant filter — registration runs pre-auth and needs to
		// see every tenant's allowlist to route the new collaborator.
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
