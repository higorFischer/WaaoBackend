using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
	public void Configure(EntityTypeBuilder<MessageAttachment> builder)
	{
		builder.ToTable("message_attachments");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Kind).HasConversion<string>().IsRequired();
		builder.Property(x => x.Url).IsRequired().HasMaxLength(2000);
		builder.Property(x => x.StorageKey).HasMaxLength(512);
		builder.Property(x => x.Mime).IsRequired().HasMaxLength(160);
		builder.Property(x => x.OriginalName).IsRequired().HasMaxLength(255).HasDefaultValue(string.Empty);

		builder.HasOne(x => x.Message)
			.WithMany(m => m.Attachments)
			.HasForeignKey(x => x.MessageId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.MessageId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
