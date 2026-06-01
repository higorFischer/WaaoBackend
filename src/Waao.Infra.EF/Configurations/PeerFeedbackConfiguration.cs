using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Feedback;

namespace Waao.Infra.EF.Configurations;

public class PeerFeedbackConfiguration : IEntityTypeConfiguration<PeerFeedback>
{
	public void Configure(EntityTypeBuilder<PeerFeedback> builder)
	{
		builder.ToTable("peer_feedbacks");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.GiverName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.RecipientName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Message).IsRequired().HasMaxLength(4000);
		builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);

		builder.HasOne(x => x.Giver).WithMany().HasForeignKey(x => x.GiverId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.RecipientId, x.CreatedAt });
		builder.HasIndex(x => new { x.GiverId, x.CreatedAt });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
