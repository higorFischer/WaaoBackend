using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
	public void Configure(EntityTypeBuilder<Channel> builder)
	{
		builder.ToTable("channels");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Name).HasMaxLength(120);
		builder.Property(x => x.Description).HasMaxLength(500);

		builder.Property(x => x.Kind)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(x => x.Scope)
			.HasConversion<string>()
			.IsRequired()
			.HasDefaultValue(ChannelScope.Custom);

		builder.HasOne(x => x.Department)
			.WithMany()
			.HasForeignKey(x => x.DepartmentId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.CreatedBy)
			.WithMany()
			.HasForeignKey(x => x.CreatedById)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.Kind, x.IsDeleted });
		builder.HasIndex(x => x.DepartmentId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ChannelMemberConfiguration : IEntityTypeConfiguration<ChannelMember>
{
	public void Configure(EntityTypeBuilder<ChannelMember> builder)
	{
		builder.ToTable("channel_members");
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Channel)
			.WithMany(c => c.Members)
			.HasForeignKey(x => x.ChannelId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.LastReadMessage)
			.WithMany()
			.HasForeignKey(x => x.LastReadMessageId)
			.OnDelete(DeleteBehavior.SetNull);

		// Unique: one row per (channel, collaborator) when not deleted
		builder.HasIndex(x => new { x.ChannelId, x.CollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => new { x.CollaboratorId, x.IsDeleted });
		builder.HasIndex(x => new { x.ChannelId, x.IsDeleted });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
	public void Configure(EntityTypeBuilder<Message> builder)
	{
		builder.ToTable("messages");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Body).IsRequired().HasMaxLength(4000);

		builder.HasOne(x => x.Channel)
			.WithMany(c => c.Messages)
			.HasForeignKey(x => x.ChannelId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Author)
			.WithMany()
			.HasForeignKey(x => x.AuthorId)
			.OnDelete(DeleteBehavior.Restrict);

		// Hot path index: history pagination
		builder.HasIndex(x => new { x.ChannelId, x.CreatedAt });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
