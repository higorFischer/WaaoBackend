using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
	public void Configure(EntityTypeBuilder<Meeting> builder)
	{
		builder.ToTable("meetings");
		builder.HasKey(x => x.Id);

		builder.HasOne(x => x.CalendarEvent)
			.WithMany()
			.HasForeignKey(x => x.CalendarEventId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Organizer)
			.WithMany()
			.HasForeignKey(x => x.OrganizerId)
			.OnDelete(DeleteBehavior.Restrict);

		// Unique: one meeting per calendar event (when not deleted)
		builder.HasIndex(x => x.CalendarEventId)
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.OrganizerId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class MeetingAttendeeConfiguration : IEntityTypeConfiguration<MeetingAttendee>
{
	public void Configure(EntityTypeBuilder<MeetingAttendee> builder)
	{
		builder.ToTable("meeting_attendees");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Rsvp)
			.HasConversion<string>()
			.IsRequired()
			.HasDefaultValue(MeetingRsvp.NoResponse);

		builder.HasOne(x => x.Meeting)
			.WithMany(m => m.Attendees)
			.HasForeignKey(x => x.MeetingId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.InvitedViaDepartment)
			.WithMany()
			.HasForeignKey(x => x.InvitedViaDepartmentId)
			.OnDelete(DeleteBehavior.SetNull);

		// Unique: one row per (meeting, collaborator) when not deleted
		builder.HasIndex(x => new { x.MeetingId, x.CollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => new { x.MeetingId, x.IsDeleted });
		builder.HasIndex(x => new { x.CollaboratorId, x.IsDeleted });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class MeetingAgendaItemConfiguration : IEntityTypeConfiguration<MeetingAgendaItem>
{
	public void Configure(EntityTypeBuilder<MeetingAgendaItem> builder)
	{
		builder.ToTable("meeting_agenda_items");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Notes).HasMaxLength(1000);

		builder.HasOne(x => x.Meeting)
			.WithMany(m => m.AgendaItems)
			.HasForeignKey(x => x.MeetingId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.MeetingId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
