using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Configurations;

public class CalendarConfiguration : IEntityTypeConfiguration<Calendar>
{
	public void Configure(EntityTypeBuilder<Calendar> builder)
	{
		builder.ToTable("calendars");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
		builder.Property(x => x.ColorHex).IsRequired().HasMaxLength(9).HasDefaultValue("#2A6B7E");
		builder.Property(x => x.Scope).HasConversion<string>().IsRequired();

		builder.HasOne(x => x.Owner)
			.WithMany()
			.HasForeignKey(x => x.OwnerId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.Department)
			.WithMany()
			.HasForeignKey(x => x.DepartmentId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasIndex(x => new { x.Scope, x.IsDeleted });
		builder.HasIndex(x => x.OwnerId);
		builder.HasIndex(x => x.DepartmentId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
	public void Configure(EntityTypeBuilder<CalendarEvent> builder)
	{
		builder.ToTable("calendar_events");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(2000);
		builder.Property(x => x.Location).HasMaxLength(200);
		builder.Property(x => x.ColorHex).HasMaxLength(9);
		builder.Property(x => x.RecurrenceRule).HasMaxLength(500);
		builder.Property(x => x.IsAllDay).HasDefaultValue(false);

		builder.HasOne(x => x.Calendar)
			.WithMany(c => c.Events)
			.HasForeignKey(x => x.CalendarId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.CreatedBy)
			.WithMany()
			.HasForeignKey(x => x.CreatedById)
			.OnDelete(DeleteBehavior.Restrict);

		// Hot path: window queries
		builder.HasIndex(x => new { x.CalendarId, x.StartsAtUtc, x.IsDeleted });
		// Cheap fetch of all recurring events for a window
		builder.HasIndex(x => x.RecurrenceRule)
			.HasFilter("recurrence_rule IS NOT NULL");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class EventOccurrenceOverrideConfiguration : IEntityTypeConfiguration<EventOccurrenceOverride>
{
	public void Configure(EntityTypeBuilder<EventOccurrenceOverride> builder)
	{
		builder.ToTable("event_occurrence_overrides");
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(2000);
		builder.Property(x => x.Location).HasMaxLength(200);
		builder.Property(x => x.ColorHex).HasMaxLength(9);
		builder.Property(x => x.IsCancelled).HasDefaultValue(false);

		builder.HasOne(x => x.Event)
			.WithMany(e => e.Overrides)
			.HasForeignKey(x => x.EventId)
			.OnDelete(DeleteBehavior.Cascade);

		// Unique constraint: one override per (event, occurrence) when not deleted
		builder.HasIndex(x => new { x.EventId, x.OriginalStartUtc })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.EventId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
