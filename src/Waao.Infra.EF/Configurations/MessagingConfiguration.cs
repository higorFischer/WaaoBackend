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

		builder.Property(x => x.IsMuted).HasDefaultValue(false);

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

		// 8000 (not 4000): bodies are encrypted at rest (AES-GCM, base64) which is ~1.4x longer than
		// the 4000-char plaintext cap. Plaintext length is still enforced by PostMessageValidator.
		builder.Property(x => x.Body).IsRequired().HasMaxLength(8000);

		builder.HasOne(x => x.Channel)
			.WithMany(c => c.Messages)
			.HasForeignKey(x => x.ChannelId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Author)
			.WithMany()
			.HasForeignKey(x => x.AuthorId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.ParentMessage)
			.WithMany()
			.HasForeignKey(x => x.ParentMessageId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Property(x => x.EditedAtUtc);

		// Hot path: history pagination ALWAYS runs through the !IsDeleted global
		// filter. A partial index that already excludes deleted rows cuts both
		// the index size and the planner's row-estimate, which matters a lot for
		// channel-list unread COUNT(*) and the last-message-per-channel query.
		builder.HasIndex(x => new { x.ChannelId, x.CreatedAt })
			.HasFilter("is_deleted = false");
		builder.HasIndex(x => x.ParentMessageId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
