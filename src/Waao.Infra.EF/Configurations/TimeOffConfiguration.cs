using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.TimeOff;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class TimeOffRequestConfiguration : IEntityTypeConfiguration<TimeOffRequest>
{
	public void Configure(EntityTypeBuilder<TimeOffRequest> builder)
	{
		builder.ToTable("time_off_requests");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.CollaboratorName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Reason).HasMaxLength(500);
		builder.Property(x => x.ReviewerName).HasMaxLength(200);
		builder.Property(x => x.ReviewNote).HasMaxLength(500);

		builder.Property(x => x.Type)
			.HasConversion<string>()
			.HasMaxLength(20);

		builder.Property(x => x.Status)
			.HasConversion<string>()
			.HasMaxLength(20);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.CollaboratorId, x.Status });
		builder.HasIndex(x => x.Status);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
