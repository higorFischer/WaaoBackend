using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Enums;
using FeedbackEntity = Waao.Domain.Models.Entities.Feedback.Feedback;

namespace Waao.Infra.EF.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<FeedbackEntity>
{
	public void Configure(EntityTypeBuilder<FeedbackEntity> builder)
	{
		builder.ToTable("feedback");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Message).IsRequired().HasMaxLength(4000);

		builder.Property(x => x.Category)
			.HasConversion<string>()
			.IsRequired()
			.HasDefaultValue(FeedbackCategory.Other);

		builder.Property(x => x.Status)
			.HasConversion<string>()
			.IsRequired()
			.HasDefaultValue(FeedbackStatus.New);

		builder.HasOne(x => x.SubmittedBy)
			.WithMany()
			.HasForeignKey(x => x.SubmittedById)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.Status);
		builder.HasIndex(x => x.Category);
		builder.HasIndex(x => x.SubmittedById);
		builder.HasIndex(x => x.CreatedAt);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
