using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities;

namespace Waao.Infra.EF.Configurations;

public class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
	public void Configure(EntityTypeBuilder<Challenge> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
		builder.Property(x => x.Category).IsRequired().HasMaxLength(80);
		builder.Property(x => x.PassPercent).HasDefaultValue(70);
		builder.Property(x => x.IsPublished).HasDefaultValue(false);

		builder.HasOne(x => x.CreatedBy)
			.WithMany()
			.HasForeignKey(x => x.CreatedById)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.IsPublished, x.IsDeleted });
		builder.HasIndex(x => x.Category);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ChallengeQuestionConfiguration : IEntityTypeConfiguration<ChallengeQuestion>
{
	public void Configure(EntityTypeBuilder<ChallengeQuestion> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Prompt).IsRequired().HasMaxLength(500);
		builder.Property(x => x.OptionA).IsRequired().HasMaxLength(200);
		builder.Property(x => x.OptionB).IsRequired().HasMaxLength(200);
		builder.Property(x => x.OptionC).IsRequired().HasMaxLength(200);
		builder.Property(x => x.OptionD).IsRequired().HasMaxLength(200);
		builder.Property(x => x.CorrectOption).HasColumnType("char(1)");

		builder.HasOne(x => x.Challenge)
			.WithMany(c => c.Questions)
			.HasForeignKey(x => x.ChallengeId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.ChallengeId, x.Order })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.ChallengeId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ChallengeAttemptConfiguration : IEntityTypeConfiguration<ChallengeAttempt>
{
	public void Configure(EntityTypeBuilder<ChallengeAttempt> builder)
	{
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.Challenge)
			.WithMany(c => c.Attempts)
			.HasForeignKey(x => x.ChallengeId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CollaboratorId, x.ChallengeId });
		builder.HasIndex(x => new { x.CollaboratorId, x.IsDeleted });

		builder.HasIndex(x => new { x.SubmittedAt, x.Passed })
			.HasFilter("passed = true AND xp_awarded_at IS NULL AND is_deleted = false");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ChallengeAttemptAnswerConfiguration : IEntityTypeConfiguration<ChallengeAttemptAnswer>
{
	public void Configure(EntityTypeBuilder<ChallengeAttemptAnswer> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.SelectedOption).HasColumnType("char(1)");

		builder.HasOne(x => x.Attempt)
			.WithMany(a => a.Answers)
			.HasForeignKey(x => x.AttemptId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Question)
			.WithMany()
			.HasForeignKey(x => x.QuestionId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.AttemptId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
