using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities;

namespace Waao.Infra.EF.Configurations;

public class CollaboratorConfiguration : IEntityTypeConfiguration<Collaborator>
{
	public void Configure(EntityTypeBuilder<Collaborator> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Cpf).HasMaxLength(14);
		builder.Property(x => x.PhotoUrl).HasMaxLength(500);
		builder.Property(x => x.Bio).HasMaxLength(2_000);
		builder.Property(x => x.Status).HasConversion<string>();
		builder.Property(x => x.RoleKind).HasConversion<string>();
		builder.Property(x => x.PasswordHash).HasMaxLength(500);
		builder.Property(x => x.CurrentLevel).HasDefaultValue(0);

		builder.Property(x => x.EmailVerified).HasDefaultValue(false);
		builder.HasIndex(x => x.EmailVerificationToken).HasFilter("email_verification_token IS NOT NULL");

		builder.HasIndex(x => x.Email).IsUnique().HasFilter("is_deleted = false");

		builder.HasOne(x => x.Department)
			.WithMany(d => d.Collaborators)
			.HasForeignKey(x => x.DepartmentId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.Role)
			.WithMany(r => r.Collaborators)
			.HasForeignKey(x => x.RoleId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.Manager)
			.WithMany(m => m.DirectReports)
			.HasForeignKey(x => x.ManagerId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
	public void Configure(EntityTypeBuilder<Department> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.HasIndex(x => x.Name).IsUnique().HasFilter("is_deleted = false");
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
	public void Configure(EntityTypeBuilder<Role> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Track).HasMaxLength(80);
		builder.HasIndex(x => new { x.Title, x.Track });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CareerEventConfiguration : IEntityTypeConfiguration<CareerEvent>
{
	public void Configure(EntityTypeBuilder<CareerEvent> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Type).HasConversion<string>();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Notes).HasMaxLength(4_000);

		builder.HasOne(x => x.Collaborator)
			.WithMany(c => c.CareerEvents)
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CollaboratorId, x.EventDate });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
	public void Configure(EntityTypeBuilder<Badge> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(80);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Category).HasConversion<string>();
		builder.Property(x => x.Rarity).HasConversion<string>();
		builder.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CollaboratorBadgeConfiguration : IEntityTypeConfiguration<CollaboratorBadge>
{
	public void Configure(EntityTypeBuilder<CollaboratorBadge> builder)
	{
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Collaborator)
			.WithMany(c => c.Badges)
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Badge)
			.WithMany(b => b.Holders)
			.HasForeignKey(x => x.BadgeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.CollaboratorId, x.BadgeId }).IsUnique().HasFilter("is_deleted = false");
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class XpTransactionConfiguration : IEntityTypeConfiguration<XpTransaction>
{
	public void Configure(EntityTypeBuilder<XpTransaction> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Source).HasConversion<string>();
		builder.Property(x => x.Reason).HasMaxLength(300);
		builder.Property(x => x.SourceEntityType).HasMaxLength(80);

		builder.HasOne(x => x.Collaborator)
			.WithMany(c => c.XpTransactions)
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CollaboratorId, x.OccurredAt });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class LevelDefinitionConfiguration : IEntityTypeConfiguration<LevelDefinition>
{
	public void Configure(EntityTypeBuilder<LevelDefinition> builder)
	{
		builder.HasKey(x => x.Id);
		builder.HasIndex(x => x.Level).IsUnique();
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
