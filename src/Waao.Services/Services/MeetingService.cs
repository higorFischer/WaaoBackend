using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class MeetingService(
	WaaoDbContext Db,
	ICalendarService CalendarService,
	INotificationService NotificationService) : IMeetingService
{
	// =====================================================================
	// CREATE
	// =====================================================================

	public async Task<MeetingDto> CreateAsync(CreateMeetingDto dto, Guid organizerId, CancellationToken ct = default)
	{
		// Ensure organizer has a personal calendar
		var calendarId = await CalendarService.EnsurePersonalCalendarAsync(organizerId, ct);

		// Fetch organizer for name
		var organizer = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == organizerId, ct)
			?? throw new KeyNotFoundException($"Organizer {organizerId} not found.");

		// Create the backing CalendarEvent
		var calEvent = new CalendarEvent
		{
			Id = Guid.CreateVersion7(),
			CalendarId = calendarId,
			CreatedById = organizerId,
			Title = dto.Title,
			Description = dto.Description,
			Location = dto.Location,
			StartsAtUtc = dto.StartsAtUtc,
			EndsAtUtc = dto.EndsAtUtc,
			IsAllDay = dto.IsAllDay,
			RecurrenceRule = dto.RecurrenceRule,
			RecurrenceEndUtc = dto.RecurrenceEndUtc,
			CreatedAt = DateTime.UtcNow,
		};
		Db.CalendarEvents.Add(calEvent);

		// Create the Meeting
		var meeting = new Meeting
		{
			Id = Guid.CreateVersion7(),
			CalendarEventId = calEvent.Id,
			OrganizerId = organizerId,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Meetings.Add(meeting);

		// Expand attendees
		var attendees = await ExpandAttendeesAsync(dto.AttendeeCollaboratorIds, dto.AttendeeDepartmentIds, organizerId, meeting.Id, ct);
		Db.MeetingAttendees.AddRange(attendees);

		// Create agenda items
		var agendaItems = dto.Agenda
			.Select((item, idx) => new MeetingAgendaItem
			{
				Id = Guid.CreateVersion7(),
				MeetingId = meeting.Id,
				Order = idx + 1,
				Title = item.Title,
				Notes = item.Notes,
				DurationMinutes = item.DurationMinutes,
				CreatedAt = DateTime.UtcNow,
			})
			.ToList();
		Db.MeetingAgendaItems.AddRange(agendaItems);

		await Db.SaveChangesAsync(ct);

		// Notify attendees (not the organizer) with MeetingInvite
		var attendeeIds = attendees
			.Where(a => a.CollaboratorId != organizerId)
			.Select(a => a.CollaboratorId)
			.ToList();

		foreach (var attendeeId in attendeeIds)
		{
			await NotificationService.CreateAsync(
				attendeeId,
				NotificationKind.MeetingInvite,
				$"You've been invited to \"{dto.Title}\"",
				$"Meeting starts at {dto.StartsAtUtc:g} UTC.",
				"meeting",
				meeting.Id,
				organizerId,
				ct);
		}

		return await BuildDtoAsync(meeting.Id, organizerId, ct);
	}

	// =====================================================================
	// GET
	// =====================================================================

	public async Task<MeetingDto> GetAsync(Guid meetingId, Guid callerId, CancellationToken ct = default)
	{
		var meeting = await Db.Meetings
			.Include(m => m.CalendarEvent)
			.Include(m => m.Organizer)
			.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
			?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		// Check access: organizer, attendee, or Admin
		if (!await CanReadMeetingAsync(meeting, callerId, ct))
			throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		return await BuildDtoAsync(meeting.Id, callerId, ct);
	}

	// =====================================================================
	// UPDATE
	// =====================================================================

	public async Task<MeetingDto> UpdateAsync(Guid meetingId, UpdateMeetingDto dto, Guid callerId, CancellationToken ct = default)
	{
		var meeting = await Db.Meetings
			.Include(m => m.CalendarEvent)
			.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
			?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		if (!await CanWriteMeetingAsync(meeting, callerId, ct))
			throw new UnauthorizedAccessException($"Caller {callerId} cannot modify meeting {meetingId}.");

		// Update backing event
		var evt = meeting.CalendarEvent;
		evt.Title = dto.Title;
		evt.Description = dto.Description;
		evt.Location = dto.Location;
		evt.StartsAtUtc = dto.StartsAtUtc;
		evt.EndsAtUtc = dto.EndsAtUtc;
		evt.IsAllDay = dto.IsAllDay;
		evt.RecurrenceRule = dto.RecurrenceRule;
		evt.RecurrenceEndUtc = dto.RecurrenceEndUtc;
		evt.UpdatedAt = DateTime.UtcNow;
		meeting.UpdatedAt = DateTime.UtcNow;

		// Diff attendees
		await DiffAttendeesAsync(meeting.Id, dto.AttendeeCollaboratorIds, dto.AttendeeDepartmentIds, meeting.OrganizerId, ct);

		// Load current attendees for notification (before diffing)
		var currentAttendeeIds = await Db.MeetingAttendees
			.Where(a => a.MeetingId == meeting.Id)
			.Select(a => a.CollaboratorId)
			.ToListAsync(ct);

		// Replace agenda items (soft-delete old, add new)
		var existing = await Db.MeetingAgendaItems
			.Where(a => a.MeetingId == meeting.Id)
			.ToListAsync(ct);
		foreach (var item in existing)
		{
			item.IsDeleted = true;
			item.DeletedAt = DateTime.UtcNow;
		}

		var newItems = dto.Agenda
			.Select((item, idx) => new MeetingAgendaItem
			{
				Id = Guid.CreateVersion7(),
				MeetingId = meeting.Id,
				Order = idx + 1,
				Title = item.Title,
				Notes = item.Notes,
				DurationMinutes = item.DurationMinutes,
				CreatedAt = DateTime.UtcNow,
			})
			.ToList();
		Db.MeetingAgendaItems.AddRange(newItems);

		await Db.SaveChangesAsync(ct);

		// Notify current attendees (not the actor) with MeetingUpdated
		foreach (var attendeeId in currentAttendeeIds.Where(id => id != callerId))
		{
			await NotificationService.CreateAsync(
				attendeeId,
				NotificationKind.MeetingUpdated,
				$"Meeting \"{dto.Title}\" was updated",
				$"The meeting details have changed. New time: {dto.StartsAtUtc:g} UTC.",
				"meeting",
				meetingId,
				callerId,
				ct);
		}

		return await BuildDtoAsync(meeting.Id, callerId, ct);
	}

	// =====================================================================
	// CANCEL
	// =====================================================================

	public async Task CancelAsync(Guid meetingId, Guid callerId, CancellationToken ct = default)
	{
		var meeting = await Db.Meetings
			.Include(m => m.CalendarEvent)
			.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
			?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		if (!await CanWriteMeetingAsync(meeting, callerId, ct))
			throw new UnauthorizedAccessException($"Caller {callerId} cannot cancel meeting {meetingId}.");

		// Capture attendee ids BEFORE soft-delete for notifications
		var cancelAttendeeIds = await Db.MeetingAttendees
			.Where(a => a.MeetingId == meetingId)
			.Select(a => a.CollaboratorId)
			.ToListAsync(ct);

		var meetingTitle = meeting.CalendarEvent?.Title ?? "meeting";

		// Soft-delete meeting
		meeting.IsDeleted = true;
		meeting.DeletedAt = DateTime.UtcNow;

		// Soft-delete attendees (ignore global filter to get all)
		var attendees = await Db.MeetingAttendees
			.IgnoreQueryFilters()
			.Where(a => a.MeetingId == meetingId)
			.ToListAsync(ct);
		foreach (var a in attendees)
		{
			a.IsDeleted = true;
			a.DeletedAt = DateTime.UtcNow;
		}

		// Soft-delete agenda items
		var agendaItems = await Db.MeetingAgendaItems
			.IgnoreQueryFilters()
			.Where(a => a.MeetingId == meetingId)
			.ToListAsync(ct);
		foreach (var item in agendaItems)
		{
			item.IsDeleted = true;
			item.DeletedAt = DateTime.UtcNow;
		}

		// Soft-delete backing event
		var evt = meeting.CalendarEvent!;
		evt.IsDeleted = true;
		evt.DeletedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		// Notify attendees (not the actor) with MeetingCancelled
		foreach (var attendeeId in cancelAttendeeIds.Where(id => id != callerId))
		{
			await NotificationService.CreateAsync(
				attendeeId,
				NotificationKind.MeetingCancelled,
				$"Meeting \"{meetingTitle}\" was cancelled",
				"The meeting has been cancelled.",
				"meeting",
				meetingId,
				callerId,
				ct);
		}
	}

	// =====================================================================
	// SET RSVP
	// =====================================================================

	public async Task<MeetingDto> SetRsvpAsync(Guid meetingId, SetRsvpDto dto, Guid callerId, CancellationToken ct = default)
	{
		var meeting = await Db.Meetings
			.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
			?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		var attendee = await Db.MeetingAttendees
			.FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.CollaboratorId == callerId, ct);

		if (attendee is null)
			throw new UnauthorizedAccessException($"Caller {callerId} is not an attendee of meeting {meetingId}.");

		attendee.Rsvp = dto.Rsvp;
		attendee.RespondedAt = DateTime.UtcNow;
		attendee.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		return await BuildDtoAsync(meeting.Id, callerId, ct);
	}

	// =====================================================================
	// LIST MY MEETINGS
	// =====================================================================

	public async Task<IReadOnlyList<MeetingDto>> ListMyMeetingsAsync(Guid callerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
	{
		// Get meeting IDs where caller is organizer or attendee
		var attendedMeetingIds = await Db.MeetingAttendees
			.Where(a => a.CollaboratorId == callerId)
			.Select(a => a.MeetingId)
			.ToListAsync(ct);

		var meetings = await Db.Meetings
			.Include(m => m.CalendarEvent)
			.Where(m =>
				(m.OrganizerId == callerId || attendedMeetingIds.Contains(m.Id)) &&
				m.CalendarEvent.StartsAtUtc <= toUtc &&
				m.CalendarEvent.EndsAtUtc >= fromUtc)
			.OrderBy(m => m.CalendarEvent.StartsAtUtc)
			.ToListAsync(ct);

		var dtos = new List<MeetingDto>();
		foreach (var m in meetings)
			dtos.Add(await BuildDtoAsync(m.Id, callerId, ct));

		return dtos;
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task<bool> CanReadMeetingAsync(Meeting meeting, Guid callerId, CancellationToken ct)
	{
		if (meeting.OrganizerId == callerId) return true;

		var isAdmin = await Db.Collaborators
			.AnyAsync(c => c.Id == callerId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
		if (isAdmin) return true;

		return await Db.MeetingAttendees
			.AnyAsync(a => a.MeetingId == meeting.Id && a.CollaboratorId == callerId, ct);
	}

	private async Task<bool> CanWriteMeetingAsync(Meeting meeting, Guid callerId, CancellationToken ct)
	{
		if (meeting.OrganizerId == callerId) return true;

		return await Db.Collaborators
			.AnyAsync(c => c.Id == callerId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
	}

	/// <summary>
	/// Expand attendees from direct collaborator ids and department ids.
	/// De-duplication: individual invite wins over department invite (InvitedViaDepartmentId = null).
	/// Organizer is always added as Going.
	/// </summary>
	private async Task<List<MeetingAttendee>> ExpandAttendeesAsync(
		IReadOnlyList<Guid> collaboratorIds,
		IReadOnlyList<Guid> departmentIds,
		Guid organizerId,
		Guid meetingId,
		CancellationToken ct)
	{
		// Map: collaboratorId -> InvitedViaDepartmentId (null = individual invite wins)
		var attendeeMap = new Dictionary<Guid, Guid?>();

		// Direct invites
		foreach (var id in collaboratorIds)
			attendeeMap[id] = null; // individual invite

		// Department invites
		if (departmentIds.Count > 0)
		{
			var deptMembers = await Db.Collaborators
				.Where(c => c.DepartmentId != null && departmentIds.Contains(c.DepartmentId!.Value) && c.Status == CollaboratorStatus.Active)
				.Select(c => new { c.Id, c.DepartmentId })
				.ToListAsync(ct);

			foreach (var member in deptMembers)
			{
				if (!attendeeMap.ContainsKey(member.Id))
					attendeeMap[member.Id] = member.DepartmentId; // department invite, only if not already individually invited
			}
		}

		// Organizer always Going (overrides any existing)
		attendeeMap[organizerId] = null;

		return attendeeMap.Select(kvp => new MeetingAttendee
		{
			Id = Guid.CreateVersion7(),
			MeetingId = meetingId,
			CollaboratorId = kvp.Key,
			Rsvp = kvp.Key == organizerId ? MeetingRsvp.Going : MeetingRsvp.NoResponse,
			InvitedViaDepartmentId = kvp.Value,
			CreatedAt = DateTime.UtcNow,
		}).ToList();
	}

	/// <summary>
	/// Diff attendees for an update: add new ones, soft-delete removed ones, keep survivors' RSVP.
	/// </summary>
	private async Task DiffAttendeesAsync(
		Guid meetingId,
		IReadOnlyList<Guid> collaboratorIds,
		IReadOnlyList<Guid> departmentIds,
		Guid organizerId,
		CancellationToken ct)
	{
		// Build new desired set (same expansion logic)
		var desiredMap = new Dictionary<Guid, Guid?>();

		foreach (var id in collaboratorIds)
			desiredMap[id] = null;

		if (departmentIds.Count > 0)
		{
			var deptMembers = await Db.Collaborators
				.Where(c => c.DepartmentId != null && departmentIds.Contains(c.DepartmentId!.Value) && c.Status == CollaboratorStatus.Active)
				.Select(c => new { c.Id, c.DepartmentId })
				.ToListAsync(ct);

			foreach (var member in deptMembers)
			{
				if (!desiredMap.ContainsKey(member.Id))
					desiredMap[member.Id] = member.DepartmentId;
			}
		}

		// Organizer always present
		desiredMap[organizerId] = null;

		// Load current (non-deleted) attendees
		var current = await Db.MeetingAttendees
			.Where(a => a.MeetingId == meetingId)
			.ToListAsync(ct);

		var currentMap = current.ToDictionary(a => a.CollaboratorId);

		// Soft-delete removed
		foreach (var existing in current)
		{
			if (!desiredMap.ContainsKey(existing.CollaboratorId))
			{
				existing.IsDeleted = true;
				existing.DeletedAt = DateTime.UtcNow;
			}
		}

		// Add new attendees
		foreach (var (colId, deptId) in desiredMap)
		{
			if (!currentMap.ContainsKey(colId))
			{
				Db.MeetingAttendees.Add(new MeetingAttendee
				{
					Id = Guid.CreateVersion7(),
					MeetingId = meetingId,
					CollaboratorId = colId,
					Rsvp = colId == organizerId ? MeetingRsvp.Going : MeetingRsvp.NoResponse,
					InvitedViaDepartmentId = deptId,
					CreatedAt = DateTime.UtcNow,
				});
			}
			// Note: survivors keep their existing RSVP (no update needed)
		}
	}

	private async Task<MeetingDto> BuildDtoAsync(Guid meetingId, Guid callerId, CancellationToken ct)
	{
		var meeting = await Db.Meetings
			.Include(m => m.CalendarEvent)
			.Include(m => m.Organizer)
			.FirstAsync(m => m.Id == meetingId, ct);

		var attendees = await Db.MeetingAttendees
			.Include(a => a.Collaborator)
			.Include(a => a.InvitedViaDepartment)
			.Where(a => a.MeetingId == meetingId)
			.ToListAsync(ct);

		var agendaItems = await Db.MeetingAgendaItems
			.Where(a => a.MeetingId == meetingId)
			.OrderBy(a => a.Order)
			.ToListAsync(ct);

		var myAttendee = attendees.FirstOrDefault(a => a.CollaboratorId == callerId);

		var tally = new RsvpTallyDto
		{
			Going = attendees.Count(a => a.Rsvp == MeetingRsvp.Going),
			Maybe = attendees.Count(a => a.Rsvp == MeetingRsvp.Maybe),
			Declined = attendees.Count(a => a.Rsvp == MeetingRsvp.Declined),
			NoResponse = attendees.Count(a => a.Rsvp == MeetingRsvp.NoResponse),
		};

		return new MeetingDto
		{
			Id = meeting.Id,
			CalendarEventId = meeting.CalendarEventId,
			OrganizerId = meeting.OrganizerId,
			OrganizerName = meeting.Organizer.FullName,
			Title = meeting.CalendarEvent.Title,
			Description = meeting.CalendarEvent.Description,
			Location = meeting.CalendarEvent.Location,
			StartsAtUtc = meeting.CalendarEvent.StartsAtUtc,
			EndsAtUtc = meeting.CalendarEvent.EndsAtUtc,
			IsAllDay = meeting.CalendarEvent.IsAllDay,
			RecurrenceRule = meeting.CalendarEvent.RecurrenceRule,
			IsRecurring = meeting.CalendarEvent.RecurrenceRule is not null,
			Attendees = attendees.Select(a => new MeetingAttendeeDto
			{
				Id = a.Id,
				CollaboratorId = a.CollaboratorId,
				CollaboratorName = a.Collaborator.FullName,
				CollaboratorPhotoUrl = a.Collaborator.PhotoUrl,
				Rsvp = a.Rsvp,
				RespondedAt = a.RespondedAt,
				InvitedViaDepartmentId = a.InvitedViaDepartmentId,
				InvitedViaDepartmentName = a.InvitedViaDepartment?.Name,
			}).ToList(),
			Agenda = agendaItems.Select(a => new MeetingAgendaItemDto
			{
				Id = a.Id,
				Order = a.Order,
				Title = a.Title,
				Notes = a.Notes,
				DurationMinutes = a.DurationMinutes,
			}).ToList(),
			RsvpTally = tally,
			MyRsvp = myAttendee?.Rsvp ?? MeetingRsvp.NoResponse,
		};
	}
}
