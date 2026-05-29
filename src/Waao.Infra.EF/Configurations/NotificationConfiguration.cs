using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Notifications;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
	public void Configure(EntityTypeBuilder<Notification> builder)
	{
		builder.ToTable("notifications");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Body).IsRequired().HasMaxLength(500);
		builder.Property(x => x.LinkType).IsRequired().HasMaxLength(20);

		builder.Property(x => x.Kind)
			.HasConversion<string>()
			.IsRequired();

		builder.HasOne(x => x.Recipient)
			.WithMany()
			.HasForeignKey(x => x.RecipientId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Actor)
			.WithMany()
			.HasForeignKey(x => x.ActorId)
			.OnDelete(DeleteBehavior.SetNull);

		// Hot path: unread-count + list
		builder.HasIndex(x => new { x.RecipientId, x.IsRead, x.IsDeleted });
		// Ordered list
		builder.HasIndex(x => new { x.RecipientId, x.CreatedAt });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
	public void Configure(EntityTypeBuilder<PushSubscription> builder)
	{
		builder.ToTable("push_subscriptions");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(500);
		builder.Property(x => x.P256dh).IsRequired().HasMaxLength(300);
		builder.Property(x => x.Auth).IsRequired().HasMaxLength(300);
		builder.Property(x => x.UserAgent).HasMaxLength(400);

		// One live subscription per endpoint
		builder.HasIndex(x => x.Endpoint)
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.CollaboratorId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class MessageMentionConfiguration : IEntityTypeConfiguration<Domain.Models.Entities.Messaging.MessageMention>
{
	public void Configure(EntityTypeBuilder<Domain.Models.Entities.Messaging.MessageMention> builder)
	{
		builder.ToTable("message_mentions");
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Message)
			.WithMany()
			.HasForeignKey(x => x.MessageId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.MentionedCollaborator)
			.WithMany()
			.HasForeignKey(x => x.MentionedCollaboratorId)
			.OnDelete(DeleteBehavior.Restrict);

		// Unique: one mention per (message, collaborator) when not deleted
		builder.HasIndex(x => new { x.MessageId, x.MentionedCollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.MessageId);
		builder.HasIndex(x => x.MentionedCollaboratorId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
