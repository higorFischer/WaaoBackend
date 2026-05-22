# WAAO — Meetings (Phase 2 of the collaboration suite)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Meetings (extends Calendar)
- **Depends on:** Phase 1 Calendar (shipped — `Calendar`, `CalendarEvent`, recurrence, `/calendar`)

## Context

Second phase of the WAAO collaboration suite. Phase 1 (Calendar) is in production. This adds Meetings on top: a meeting is a calendar event enriched with an organizer, attendees, RSVP, and a structured agenda. Phase 3 (in-app video via Jitsi) and Phase 4 (messaging) remain separate future specs.

## Goal

Let collaborators schedule meetings: pick a time (with optional recurrence), invite individuals and/or whole departments, collect RSVPs, and attach a structured agenda. Meetings surface on every attendee's `/calendar`.

## Locked decisions

| # | Decision |
|---|----------|
| Data model | Separate `Meeting` entity, 1:1 with a `CalendarEvent`. Plain events have no `Meeting`. Creating a meeting creates both rows in one transaction. |
| Attendees | Invite individuals AND/OR whole departments. Department invites expand to individual attendee rows **at invite time** (snapshot — later joiners are not auto-added). |
| RSVP | `NoResponse` (default) / `Going` / `Maybe` / `Declined`. |
| Recurrence + RSVP | Backing event may recur; RSVP is **one row per attendee for the whole series**. Per-occurrence RSVP deferred. |
| Agenda | Ordered list of structured `MeetingAgendaItem`s (title + optional notes + optional duration). |
| Edit rights | Organizer or Admin can edit/cancel. Any attendee can set their own RSVP — nothing else. |
| Calendar surfacing | Backing event lives on the organizer's personal calendar; attendees see it because the occurrences query also returns events backing meetings they are invited to. |

## Backend (`Waao.API` / `.Services` / `.Services.Abstractions` / `.Domain.Models` / `.Infra.EF`)

### Entities

`Meeting`:
- `Id` (Guid v7), `CalendarEventId` (Guid, FK → CalendarEvent, **unique**), `OrganizerId` (Guid → Collaborator)
- audit + soft-delete
- Title / description / location / start-end / recurrence all live on the backing `CalendarEvent` — not duplicated here.

`MeetingAttendee`:
- `Id` (Guid v7), `MeetingId` (FK), `CollaboratorId` (Guid → Collaborator)
- `Rsvp` (enum `MeetingRsvp`: `NoResponse=0`, `Going=1`, `Maybe=2`, `Declined=3`, default `NoResponse`)
- `RespondedAt` (DateTime, nullable)
- `InvitedViaDepartmentId` (Guid?, → Department — set when the attendee was added through a department invite; null when invited individually)
- audit + soft-delete; unique `(MeetingId, CollaboratorId)` where not deleted

`MeetingAgendaItem`:
- `Id` (Guid v7), `MeetingId` (FK), `Order` (int), `Title` (≤200), `Notes` (≤1000, nullable), `DurationMinutes` (int?, nullable, ≥0)
- audit + soft-delete

### Enum

`MeetingRsvp` in `Waao.Domain.Models/Enums/` — stored as string via `HasConversion<string>()`.

### Migration

`AddMeetings` — additive. Indexes:
- `Meetings.CalendarEventId` unique; `Meetings.OrganizerId`
- `MeetingAttendees.(MeetingId, IsDeleted)`, `MeetingAttendees.(CollaboratorId, IsDeleted)` — the latter powers "meetings I'm invited to"
- `MeetingAgendaItems.MeetingId`

Applied locally to `WaaoLocal`; drift-checked.

### DTOs (`Waao.Services.Abstractions/Dtos/Meetings/`)

- `MeetingDto` — `{ Id, CalendarEventId, OrganizerId, OrganizerName, Title, Description, Location, StartsAtUtc, EndsAtUtc, IsAllDay, RecurrenceRule, IsRecurring, Attendees: MeetingAttendeeDto[], Agenda: MeetingAgendaItemDto[], RsvpTally: { going, maybe, declined, noResponse }, MyRsvp }`
- `MeetingAttendeeDto` — `{ Id, CollaboratorId, CollaboratorName, CollaboratorPhotoUrl, Rsvp, RespondedAt, InvitedViaDepartmentId, InvitedViaDepartmentName }`
- `MeetingAgendaItemDto` — `{ Id, Order, Title, Notes, DurationMinutes }`
- `CreateMeetingDto` — `{ Title, Description?, Location?, StartsAtUtc, EndsAtUtc, IsAllDay, RecurrenceRule?, RecurrenceEndUtc?, AttendeeCollaboratorIds: Guid[], AttendeeDepartmentIds: Guid[], Agenda: CreateAgendaItemDto[] }`
- `CreateAgendaItemDto` — `{ Title, Notes?, DurationMinutes? }`
- `UpdateMeetingDto` — same shape as create (full replace of attendees + agenda; service diffs).
- `SetRsvpDto` — `{ MeetingRsvp Rsvp }`

### Validators (FluentValidation, `Waao.Services/Validation/Meetings/`)

- `CreateMeetingValidator` / `UpdateMeetingValidator`: Title NotEmpty ≤200; EndsAtUtc ≥ StartsAtUtc; Description ≤2000; Location ≤200; valid RRULE when set; at least one attendee (collaborator or department) after expansion; each agenda item Title NotEmpty ≤200, Notes ≤1000, DurationMinutes ≥ 0.
- `SetRsvpValidator`: `Rsvp` in enum, must not be `NoResponse` (responding means picking one of the three).

### Services

`IMeetingService`:
- `CreateAsync(CreateMeetingDto, organizerId, ct)` → `MeetingDto`
  - Creates a `CalendarEvent` on the organizer's personal calendar (lazily ensure it exists), then the `Meeting`, then expands attendees:
    - direct collaborator ids → `MeetingAttendee` rows (`InvitedViaDepartmentId = null`)
    - department ids → load active members of each department → `MeetingAttendee` rows (`InvitedViaDepartmentId = <dept>`); de-dupe so a collaborator invited both ways gets one row (individual invite wins — `InvitedViaDepartmentId = null`)
    - the organizer is always added as an attendee with `Rsvp = Going`
  - Creates `MeetingAgendaItem`s with sequential `Order`.
- `GetAsync(meetingId, callerId, ct)` → `MeetingDto` — visible if caller is organizer, an attendee, or Admin; else 404. `MyRsvp` reflects the caller.
- `UpdateAsync(meetingId, UpdateMeetingDto, callerId, ct)` — organizer or Admin only (else 403). Updates the backing event fields; re-expands attendees (new ones added with `NoResponse`; removed ones soft-deleted; surviving ones keep their RSVP); replaces agenda items.
- `CancelAsync(meetingId, callerId, ct)` — organizer or Admin; soft-deletes the `Meeting`, its attendees, its agenda items, and the backing `CalendarEvent`.
- `SetRsvpAsync(meetingId, SetRsvpDto, callerId, ct)` — caller must be an attendee; sets their `Rsvp` + `RespondedAt`. Returns updated `MeetingDto`.
- `ListMyMeetingsAsync(callerId, fromUtc, toUtc, ct)` — meetings the caller organizes or attends, in a window.

### Calendar integration

`CalendarEventService.GetOccurrencesAsync` is extended: in addition to events on the caller's visible calendars, it also includes events that back a `Meeting` the caller attends or organizes. `CalendarOccurrenceDto` gains `MeetingId` (Guid?, null for plain events) so the frontend can render a meeting marker and open the meeting detail panel on click.

### Controllers

`MeetingsController` `[Route("api/waao/meetings")] [Authorize]`:
- `POST ""` — create
- `GET "{id:guid}"` — detail
- `PUT "{id:guid}"` — update
- `DELETE "{id:guid}"` — cancel
- `POST "{id:guid}/rsvp"` — set own RSVP
- `GET "mine?fromUtc=&toUtc="` — my meetings in a window

One-line expression-bodied; `[ProducesResponseType]`; `{id:guid}`.

### DI / Program.cs

Register `IMeetingService` (Scoped).

## Frontend (`WaaoFrontend`)

- Types: `src/types/meeting.types.ts` — `Meeting`, `MeetingAttendee`, `MeetingAgendaItem`, `MeetingRsvp`, `CreateMeetingDto`, etc.
- Service: `src/services/meeting.service.ts` — create / get / update / cancel / setRsvp / listMine.
- `CalendarOccurrence` type gains `meetingId?: string | null`.
- **Meeting dialog** (`src/pages/calendar/meeting-dialog.tsx`) — portal modal: title, description, location, start/end, all-day, recurrence editor (reuse Phase 1's `RecurrenceEditor`), attendee picker (search collaborators + add department chips that show "(N people)"), structured agenda editor (add / reorder / remove items, each with title + notes + duration).
- **Meeting detail panel** (`src/pages/calendar/meeting-detail.tsx`) — portal panel: agenda list, attendees grouped by RSVP with avatars, the tally line, and the caller's RSVP control (Going / Maybe / Declined buttons; current choice highlighted). Organizer/Admin see Edit + Cancel.
- `/calendar` integration: meeting occurrences render with a distinct marker (icon + accent); clicking a meeting opens the detail panel instead of the plain event dialog. The calendar's "New" action gets a second option "New meeting" alongside "New event".
- i18n: `meetings.*` namespace × 3 locales (pt-BR canonical, genuine en + es).
- Reuse the Phase 1 portal modal pattern and `PortalMenu`.

## Error contract

| Case | HTTP |
|------|------|
| Create/update invalid (bad RRULE, no attendees, bad agenda) | 400 FluentValidation |
| Get/update/cancel/rsvp unknown meeting | 404 |
| Get a meeting the caller doesn't organize/attend (non-Admin) | 404 |
| Update/cancel by a non-organizer non-Admin | 403 |
| RSVP by a non-attendee | 403 |
| `SetRsvpDto.Rsvp = NoResponse` | 400 |

## Testing (`Waao.Tests`, EF InMemory)

- Create: backing event + meeting created; organizer auto-added as `Going`; department invite expands to member rows tagged with `InvitedViaDepartmentId`; collaborator invited both individually and via department gets ONE row with `InvitedViaDepartmentId = null`.
- `GetAsync`: visible to organizer/attendee/Admin; 404 for an unrelated collaborator; `MyRsvp` correct per caller.
- Update: added attendees start `NoResponse`; removed attendees soft-deleted; surviving attendees keep RSVP; agenda replaced.
- `SetRsvp`: attendee can set; non-attendee → 403; `NoResponse` rejected; `RespondedAt` stamped.
- Cancel: organizer/Admin can; non-organizer → 403; meeting + attendees + agenda + backing event all soft-deleted.
- Calendar integration: an invited attendee's `GetOccurrencesAsync` returns the meeting's event even though it sits on the organizer's personal calendar; the occurrence carries `MeetingId`.
- Recurring meeting: one RSVP applies across occurrences.

## Rollout / safety

- New tables only — additive migration, no destructive ops, no backfill. Applied under the startup advisory lock.
- Deploys via existing CI/CD (backend push → Fly; frontend push → Cloudflare).
- Backward compatible: plain calendar events untouched; `CalendarOccurrenceDto.MeetingId` is nullable and defaults null.

## Out of scope (Phase 2 — deferred)

- In-app video / "Join" button — **Phase 3 (Jitsi embed)**
- Notifications / email invites / reminders — rides with the notifications work
- Per-occurrence RSVP for recurring meetings
- Attendees proposing alternative times
- Meeting notes / minutes capture during the meeting
- External calendar (Google/Outlook) invites or ICS export
