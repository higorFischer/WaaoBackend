# WAAO — Calendar (Phase 1 of the collaboration suite)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Calendar

## Context

WAAO is growing a collaboration suite. The full vision (a Slack-style chat + calendar + in-app video meetings) is decomposed into independent phases, each with its own spec → plan → build cycle:

| Phase | Sub-project | Status |
|-------|-------------|--------|
| **1** | **Calendar** | this spec |
| 2 | Meetings (calendar event + attendees + RSVP + agenda) | future spec |
| 3 | Video (Jitsi embed room per meeting) | future spec |
| 4 | Messaging (channels + DMs) | separate track, future spec |

This document covers **Phase 1 only**. Meetings, video, attendees, RSVP, reminders and chat are explicitly out of scope here.

## Goal

A calendar inside WAAO with personal, department, and company calendars; create/edit/delete events; full iCalendar-style recurrence with per-occurrence overrides; month / week / agenda views.

## Locked decisions

| # | Decision |
|---|----------|
| Calendar kinds | Personal (auto, per collaborator), Department (one per Department), Company (one global) |
| Shared edit rights | Anyone within a shared calendar's scope can create/edit/delete events on it |
| Recurrence | Full — RRULE base rule + per-occurrence overrides + edit-scope (this / this-and-future / all) |
| Time storage | UTC in the DB; rendered in the browser's local timezone |
| Attendees / RSVP / reminders / video | OUT of scope — Phase 2+ |

## Backend (`Waao.API` / `.Services` / `.Services.Abstractions` / `.Domain.Models` / `.Infra.EF`)

### Entities

`Calendar`:
- `Id` (Guid v7), `Name` (≤120), `ColorHex` (≤9, default `#2A6B7E`)
- `Scope` (enum `CalendarScope`: `Personal=0`, `Department=1`, `Company=2`)
- `OwnerId` (Guid?, → Collaborator — set for Personal, null otherwise)
- `DepartmentId` (Guid?, → Department — set for Department scope, null otherwise)
- audit + soft-delete

`CalendarEvent`:
- `Id` (Guid v7), `CalendarId` (FK), `CreatedById` (Guid → Collaborator)
- `Title` (≤200), `Description` (≤2000, nullable), `Location` (≤200, nullable)
- `StartsAtUtc` (DateTime), `EndsAtUtc` (DateTime), `IsAllDay` (bool, default false)
- `ColorHex` (≤9, nullable — overrides the calendar's color when set)
- `RecurrenceRule` (≤500, nullable — RRULE string, e.g. `FREQ=WEEKLY;BYDAY=MO`; null = single event)
- `RecurrenceEndUtc` (DateTime, nullable — series stop date; null + a rule = open-ended, capped at expansion time)
- audit + soft-delete

`EventOccurrenceOverride`:
- `Id` (Guid v7), `EventId` (FK → CalendarEvent)
- `OriginalStartUtc` (DateTime — identifies WHICH occurrence of the series this overrides)
- `IsCancelled` (bool, default false — true = this occurrence is removed)
- Override fields (all nullable; null = inherit from the base event): `Title`, `Description`, `Location`, `StartsAtUtc`, `EndsAtUtc`, `IsAllDay`, `ColorHex`
- audit + soft-delete
- Unique `(EventId, OriginalStartUtc)` where not deleted

### Migration

`AddCalendar` — additive. Indexes:
- `Calendars.(Scope, IsDeleted)`, `Calendars.OwnerId`, `Calendars.DepartmentId`
- `CalendarEvents.(CalendarId, StartsAtUtc, IsDeleted)` — the hot path for window queries
- `CalendarEvents.RecurrenceRule` partial: `WHERE recurrence_rule IS NOT NULL` — fetch all recurring events for a window cheaply
- `EventOccurrenceOverrides.EventId`

Applied locally to `WaaoLocal`; drift-checked before commit.

### Recurrence expansion

A recurring event is ONE `CalendarEvent` row carrying `RecurrenceRule`. The backend expands it into concrete occurrences for the requested window.

- **Library:** use a maintained .NET RRULE library — `Ical.Net` (NuGet, MIT) — for parsing/expanding RRULE. Do NOT hand-roll recurrence math.
- **Expansion service:** `IRecurrenceExpander` — given a `CalendarEvent` + `[windowStart, windowEnd]`, returns the list of occurrence start times within the window, capped (hard cap e.g. 366 occurrences per event per query to bound cost).
- **Applying overrides:** for each expanded occurrence start, look up an `EventOccurrenceOverride` keyed by `(EventId, OriginalStartUtc)`. If `IsCancelled` → drop the occurrence. Else → merge non-null override fields over the base event.
- A non-recurring event yields exactly one occurrence (itself), still subject to overrides (none expected, but uniform code path).

### Edit-scope semantics (PUT / DELETE)

`editScope` query param: `this` | `thisAndFuture` | `all` (single events ignore it → treated as `all`).

- **`this`** — write/update an `EventOccurrenceOverride` for that `OriginalStartUtc` (DELETE → override with `IsCancelled=true`).
- **`thisAndFuture`** — set the base event's `RecurrenceEndUtc` to just before the target occurrence; create a NEW `CalendarEvent` starting at the target occurrence with the new field values + the (possibly new) recurrence rule. Existing overrides whose `OriginalStartUtc >= split point` move to the new event.
- **`all`** — update the base `CalendarEvent` row directly. DELETE → soft-delete the base event AND its overrides.

### DTOs (`Waao.Services.Abstractions/Dtos/Calendar/`)

- `CalendarDto { Id, Name, ColorHex, Scope, OwnerId, DepartmentId, DepartmentName, CanEdit }`
- `CalendarEventDto` — a single base event (admin/edit views): all fields incl. `RecurrenceRule`, `RecurrenceEndUtc`.
- `CalendarOccurrenceDto` — ONE expanded occurrence for rendering: `{ EventId, OccurrenceStartUtc, OriginalStartUtc, Title, Description, Location, StartsAtUtc, EndsAtUtc, IsAllDay, EffectiveColorHex, CalendarId, IsRecurring, IsOverride }`.
- `CreateCalendarEventDto`, `UpdateCalendarEventDto` (includes optional recurrence fields).
- `EventWindowQueryDto { DateTime FromUtc, DateTime ToUtc, Guid[]? CalendarIds }`.

### Validators (FluentValidation, `Waao.Services/Validation/Calendar/`)

- `CreateCalendarEventValidator` / `UpdateCalendarEventValidator`: Title NotEmpty ≤200; EndsAtUtc ≥ StartsAtUtc; Description ≤2000; Location ≤200; if `RecurrenceRule` set, it must parse as a valid RRULE (via Ical.Net) and the requested window cap must not be exceeded.
- Window query: `ToUtc > FromUtc`, span ≤ 366 days (bounds expansion cost).

### Services

- `ICalendarService`:
  - `EnsurePersonalCalendarAsync(collaboratorId, ct)` — lazily creates a collaborator's personal calendar on first use.
  - `ListVisibleCalendarsAsync(collaboratorId, ct)` → personal + their department's + company. Sets `CanEdit` (always true under the "anyone in scope" rule).
  - Department + Company calendars are created by a `DbInitializer` seed (`SeedCalendarsAsync`): one Company calendar, one per existing Department. New departments get a calendar created on the fly by a hook in `AdminService.CreateDepartmentAsync` (small addition to that method).
- `ICalendarEventService`:
  - `GetOccurrencesAsync(EventWindowQueryDto, collaboratorId, ct)` → `CalendarOccurrenceDto[]` — visibility-filtered to calendars the caller can see; expands recurrence; applies overrides.
  - `GetEventAsync(eventId, collaboratorId, ct)` → `CalendarEventDto` (the base row, for the edit form).
  - `CreateAsync(CreateCalendarEventDto, collaboratorId, ct)`.
  - `UpdateAsync(eventId, UpdateCalendarEventDto, editScope, originalStartUtc?, collaboratorId, ct)`.
  - `DeleteAsync(eventId, editScope, originalStartUtc?, collaboratorId, ct)`.
  - All writes verify the caller is within the target calendar's scope.
- `IRecurrenceExpander` — wraps Ical.Net; pure function, unit-testable in isolation.

### Controllers

`CalendarsController` `[Route("api/waao/calendars")] [Authorize]`:
- `GET ""` — visible calendars.

`CalendarEventsController` `[Route("api/waao/calendar/events")] [Authorize]`:
- `GET ""` — `[FromQuery] EventWindowQueryDto` → occurrences.
- `GET "{id:guid}"` — base event for editing.
- `POST ""` — create.
- `PUT "{id:guid}"` — `[FromQuery] editScope`, `[FromQuery] originalStartUtc?`.
- `DELETE "{id:guid}"` — `[FromQuery] editScope`, `[FromQuery] originalStartUtc?`.

One-line expression-bodied actions; `[ProducesResponseType]` on each; `{id:guid}` constraint.

### DI / Program.cs

Register `ICalendarService`, `ICalendarEventService`, `IRecurrenceExpander` (Scoped, expander can be Singleton — pure). Add `Ical.Net` NuGet package to `Waao.Services.csproj`.

## Frontend (`WaaoFrontend`)

- New route `/calendar` (authed, inside `AuthedShell`).
- Sidebar: new nav item "Calendário / Calendar / Calendario" under a sensible section (e.g. "Trabalho"/Work). Add to CommandPalette.
- Pages / components:
  - `CalendarPage` — top bar (view switch Month/Week/Agenda, date nav, "New event"), left sidebar listing visible calendars with show/hide checkboxes + color dots, main grid.
  - `MonthView`, `WeekView`, `AgendaView` — render `CalendarOccurrenceDto[]` for the current window.
  - `EventDialog` — create/edit form (portal-based, per the `board-edit-dialog` pattern): title, description, location, start/end, all-day toggle, calendar picker, color, recurrence editor.
  - `RecurrenceEditor` — frequency (none/daily/weekly/monthly), interval, by-weekday (for weekly), end (never / on-date / after-N), producing an RRULE string.
  - `EditScopePrompt` — when editing/deleting an occurrence of a recurring event, a small portal dialog: "This event / This and following / All events".
- Data: TanStack Query keyed by `['calendar','events',fromIso,toIso,calendarIds]`; refetch on window change; invalidate on mutation.
- Service: `calendar.service.ts` (list calendars, get occurrences, get event, create, update, delete).
- Types: `src/types/calendar.types.ts`.
- i18n: `calendar.*` namespace × 3 locales (pt-BR canonical, genuine en + es) — views, weekday/month names via `Intl`, form labels, recurrence editor, edit-scope prompt.
- Reuse the existing `PortalMenu` for any dropdowns; reuse the portal modal pattern for dialogs.

## Error contract

| Case | HTTP |
|------|------|
| Create/update invalid fields (incl. bad RRULE) | 400 FluentValidation `errors.<field>` |
| Window query span > 366 days | 400 |
| Unknown event / calendar | 404 |
| Caller not in the target calendar's scope (write) | 403 |
| `editScope=this`/`thisAndFuture` without `originalStartUtc` | 400 |

## Testing (`Waao.Tests`, EF InMemory)

- **`IRecurrenceExpander`** (highest-value tests): weekly/daily/monthly expansion; window boundaries (occurrences exactly on from/to); `RecurrenceEndUtc` honored; 366-cap enforced.
- Override application: cancelled occurrence dropped; field-level override merged; non-recurring event yields one occurrence.
- Edit-scope: `this` writes an override; `thisAndFuture` truncates base + creates new series + migrates later overrides; `all` mutates the base.
- Visibility: personal calendar invisible to others; department calendar visible to department members only; company visible to all; write outside scope → 403.
- Validators: end-before-start rejected; invalid RRULE rejected; window cap.

## Rollout / safety

- New tables only — additive migration, no destructive ops, no backfill. Applied under the existing startup advisory lock.
- `SeedCalendarsAsync` in `DbInitializer`: idempotent — creates the Company calendar + one per existing Department only when absent.
- Deploys via existing CI/CD (backend push → Fly auto-deploy; frontend push → Cloudflare).

## Out of scope (Phase 1 — explicitly deferred)

- Attendees, invitations, RSVP — Phase 2 (Meetings)
- Reminders / notifications — rides with the notifications work
- In-app video — Phase 3 (Jitsi embed)
- Chat / channels / DMs — Phase 4 (Messaging), separate track
- External calendar sync (Google/Outlook/ICS import-export)
- Drag-to-move / drag-to-resize events — may be a fast-follow after Phase 1 ships
- Timezone selection per event (everything is UTC-stored, local-rendered)
