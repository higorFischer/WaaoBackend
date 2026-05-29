using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.FeatureRequests;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class FeatureRequestConfiguration : IEntityTypeConfiguration<FeatureRequest>
{
	public void Configure(EntityTypeBuilder<FeatureRequest> builder)
	{
		builder.ToTable("feature_requests");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Title).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Description).IsRequired().HasMaxLength(4000);
		builder.Property(x => x.AdminResponse).HasMaxLength(2000);

		builder.Property(x => x.Status)
			.HasConversion<string>()
			.IsRequired()
			.HasDefaultValue(FeatureRequestStatus.New);

		builder.HasOne(x => x.SubmittedBy)
			.WithMany()
			.HasForeignKey(x => x.SubmittedById)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.Status);
		builder.HasIndex(x => x.SubmittedById);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class FeatureRequestVoteConfiguration : IEntityTypeConfiguration<FeatureRequestVote>
{
	public void Configure(EntityTypeBuilder<FeatureRequestVote> builder)
	{
		builder.ToTable("feature_request_votes");
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.FeatureRequest)
			.WithMany(r => r.Votes)
			.HasForeignKey(x => x.FeatureRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Restrict);

		// One vote per (request, collaborator) when not deleted
		builder.HasIndex(x => new { x.FeatureRequestId, x.CollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class FeatureRequestCommentConfiguration : IEntityTypeConfiguration<FeatureRequestComment>
{
	public void Configure(EntityTypeBuilder<FeatureRequestComment> builder)
	{
		builder.ToTable("feature_request_comments");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Body).IsRequired().HasMaxLength(2000);

		builder.HasOne(x => x.FeatureRequest)
			.WithMany(r => r.Comments)
			.HasForeignKey(x => x.FeatureRequestId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Author)
			.WithMany()
			.HasForeignKey(x => x.AuthorId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.FeatureRequestId, x.CreatedAt });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
