using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Messaging;

namespace Waao.Infra.EF.Configurations;

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
	public void Configure(EntityTypeBuilder<MessageReaction> builder)
	{
		builder.ToTable("message_reactions");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Emoji).IsRequired().HasMaxLength(32);

		builder.HasOne(x => x.Message)
			.WithMany()
			.HasForeignKey(x => x.MessageId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.MessageId, x.CollaboratorId, x.Emoji })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.MessageId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
