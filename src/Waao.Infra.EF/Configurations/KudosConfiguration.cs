using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Kudos;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class KudoConfiguration : IEntityTypeConfiguration<Kudo>
{
	public void Configure(EntityTypeBuilder<Kudo> builder)
	{
		builder.ToTable("kudos");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.GiverName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.GiverPhotoUrl).HasMaxLength(1000);
		builder.Property(x => x.Message).IsRequired().HasMaxLength(500);

		builder.Property(x => x.Value)
			.HasConversion<string>()
			.HasMaxLength(20);

		builder.HasOne(x => x.Giver)
			.WithMany()
			.HasForeignKey(x => x.GiverId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasMany(x => x.Recipients)
			.WithOne(r => r.Kudo)
			.HasForeignKey(r => r.KudoId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.GiverId);
		builder.HasIndex(x => x.CreatedAt);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class KudoRecipientConfiguration : IEntityTypeConfiguration<KudoRecipient>
{
	public void Configure(EntityTypeBuilder<KudoRecipient> builder)
	{
		builder.ToTable("kudo_recipients");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.CollaboratorName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.CollaboratorPhotoUrl).HasMaxLength(1000);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.CollaboratorId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
