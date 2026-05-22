# WAAO — Mentions & Notifications (Phase 4b of the collaboration suite)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Messaging / Notifications
- **Depends on:** Phase 4a Messaging (shipped — channels, messages, `MessagingHub`), Phase 2 Meetings

## Goal

`@mention` people in channel messages, and a notification surface (bell + unread count + panel) that mentions and key messaging/meeting events feed, delivered live over the existing SignalR hub.

## Locked decisions

| # | Decision |
|---|----------|
| Notification events | `Mention` (in a message), `ChannelInvite` (added to a private channel), `MeetingInvite`, `MeetingUpdated`, `MeetingCancelled` |
| Mention input | Autocomplete picker — typing `@` in the composer opens a channel-member dropdown; picking inserts a structured token |
| Mention token format | `@[Display Name](collaboratorId)` embedded in the message body |
| Delivery | A bell UI with unread badge + panel, AND live push via the existing `MessagingHub` (per-user group) |

## Backend (`Waao.API` / `.Services` / `.Services.Abstractions` / `.Domain.Models` / `.Infra.EF`)

### Enum

`NotificationKind { Mention=0, ChannelInvite=1, MeetingInvite=2, MeetingUpdated=3, MeetingCancelled=4 }` in `Waao.Domain.Models/Enums/` — stored as string via `HasConversion<string>()`.

### Entities (`Waao.Domain.Models/Entities/Notifications/`)

`Notification`:
- `Id` (Guid v7), `RecipientId` (Guid → Collaborator)
- `Kind` (NotificationKind)
- `Title` (≤200), `Body` (≤500)
- `LinkType` (≤20 — `"channel"` or `"meeting"`), `LinkId` (Guid)
- `ActorId` (Guid?, → Collaborator who caused it — null for system events)
- `IsRead` (bool, default false), `ReadAt` (DateTime?)
- audit + soft-delete

`MessageMention` (`Waao.Domain.Models/Entities/Messaging/`):
- `Id` (Guid v7), `MessageId` (FK → Message), `MentionedCollaboratorId` (Guid → Collaborator)
- audit + soft-delete; unique `(MessageId, MentionedCollaboratorId)` where not deleted

### Migration

`AddNotifications` — additive. Indexes:
- `Notifications.(RecipientId, IsRead, IsDeleted)` — the unread-count + list hot path
- `Notifications.(RecipientId, CreatedAt)` — ordered list
- `MessageMentions.MessageId`, `MessageMentions.MentionedCollaboratorId`

Applied locally to `WaaoLocal`; drift-checked.

### DTOs (`Waao.Services.Abstractions/Dtos/Notifications/`)

- `NotificationDto` — `{ Id, Kind, Title, Body, LinkType, LinkId, ActorId, ActorName, ActorPhotoUrl, IsRead, CreatedAtUtc }`
- `NotificationListDto` — `{ Items: NotificationDto[], UnreadCount: int }`

`MessageDto` (existing, Phase 4a) gains `Mentions: MessageMentionDto[]` where `MessageMentionDto = { MentionedCollaboratorId, MentionedCollaboratorName }` — lets the client render mention chips without re-parsing.

### Services

`INotificationService`:
- `CreateAsync(Guid recipientId, NotificationKind kind, string title, string body, string linkType, Guid linkId, Guid? actorId, CancellationToken ct)` — persists a `Notification`; then broadcasts `notificationReceived` to the recipient's SignalR group `user:{recipientId}` via `IHubContext<MessagingHub>`. No-op if `recipientId == actorId` (never notify yourself).
- `ListAsync(Guid collaboratorId, bool unreadOnly, CancellationToken ct)` → `NotificationListDto` (newest first, cap 100, always carries `UnreadCount`).
- `MarkReadAsync(Guid notificationId, Guid collaboratorId, CancellationToken ct)` — caller must be the recipient.
- `MarkAllReadAsync(Guid collaboratorId, CancellationToken ct)`.

`INotificationService` is injected into the services that raise events.

### Mention parsing

- A static `MentionParser.ExtractCollaboratorIds(string body)` — regex `@\[[^\]]+\]\(([0-9a-fA-F-]{36})\)` → distinct `Guid` list.
- `MessageService.PostMessageAsync` (Phase 4a) is extended: after persisting the `Message`, extract mention ids, keep only those that are **members of the channel** AND **not the author**, write `MessageMention` rows, and call `INotificationService.CreateAsync(... Mention ...)` for each. The notification `Body` is a snippet of the message; `LinkType="channel"`, `LinkId=channelId`.

### Event wiring

- `ChannelService.AddMemberAsync` → `ChannelInvite` notification to the added collaborator (skip for public-channel self-joins — only fired on the add-member path; `actorId` = the member who added them).
- `MeetingService.CreateAsync` → `MeetingInvite` to every attendee except the organizer.
- `MeetingService.UpdateAsync` → `MeetingUpdated` to all current attendees except the actor.
- `MeetingService.CancelAsync` → `MeetingCancelled` to all attendees except the actor. `LinkType="meeting"`, `LinkId=meetingId`.

### SignalR

- `MessagingHub.OnConnectedAsync` (Phase 4a) is extended: also add the connection to a personal group `user:{collaboratorId}`.
- New server→client event name: `notificationReceived` with a `NotificationDto` payload — broadcast by `NotificationService.CreateAsync`.

### Controllers

`NotificationsController` `[Route("api/waao/notifications")] [Authorize]`:
- `GET ""` — `[FromQuery] bool unreadOnly` → `NotificationListDto`
- `GET "unread-count"` → `{ count }`
- `POST "{id:guid}/read"` — mark one read
- `POST "read-all"` — mark all read

### DI / Program.cs

Register `INotificationService` (Scoped).

## Frontend (`WaaoFrontend`)

- Types: `src/types/notification.types.ts`. `MessageMention` added to `messaging.types.ts` `Message`.
- Service: `src/services/notification.service.ts` — list / unreadCount / markRead / markAllRead.
- `src/lib/messaging-hub.ts` (Phase 4a) extended — subscribe to `notificationReceived`.
- **Bell** (`src/components/notifications/notification-bell.tsx`) — placed in the sidebar by the user-profile block; unread-count badge; opens a portal dropdown panel.
- **Notification panel** (`src/components/notifications/notification-panel.tsx`) — list (unread emphasized), each row: actor avatar + title/body + relative time; clicking marks read and navigates (`channel` → `/messages` with that channel open; `meeting` → `/calendar` with the meeting detail open); "Mark all read" action. Live: `notificationReceived` prepends + bumps the badge.
- **Composer @mention** — extend the Phase 4a `MessageComposer`: typing `@` opens an inline member-autocomplete (channel members, filtered); selecting inserts `@[Name](id)` into the body. Backspace over a token removes it cleanly.
- **Timeline mention chips** — `ConversationPane` renders `@[Name](id)` tokens as highlighted chips (using `MessageDto.Mentions` for the display names); a chip for the current user gets an extra accent.
- i18n: `notifications.*` namespace × 3 locales (pt-BR canonical, genuine en + es).
- Reuse the portal pattern + `PortalMenu`.

## Error contract

| Case | HTTP |
|------|------|
| Mark-read a notification that isn't yours | 403 |
| Unknown notification | 404 |

## Testing (`Waao.Tests`, EF InMemory)

- `MentionParser` — extracts ids from valid tokens; ignores malformed; de-dupes.
- `PostMessage` with a mention of a channel member → `MessageMention` row + `Mention` notification created; mention of a NON-member → no notification; self-mention → no notification.
- `ChannelService.AddMember` → `ChannelInvite` notification for the added user.
- `MeetingService` Create/Update/Cancel → the right notification kind for attendees, never for the actor.
- `NotificationService` — `ListAsync` `unreadOnly` filter + `UnreadCount`; `MarkRead` sets `IsRead`/`ReadAt`; `MarkRead` of another user's notification → 403; `MarkAllRead`.
- `CreateAsync` is a no-op when `recipientId == actorId`.

## Rollout / safety

- New tables only — additive migration, no destructive ops, no backfill. Applied under the startup advisory lock.
- `waao-api` stays at a single Fly machine (SignalR, Phase 4a constraint — unchanged).
- Backend push → Fly CI/CD; frontend push → Cloudflare.

## Out of scope (4b — deferred)

- Email / browser-push notifications
- Notification preferences / per-channel muting / do-not-disturb
- XP, badge, career-event, new-course/challenge events feeding the bell (possible later slice)
- Digest / daily-summary notifications
- `@channel` / `@here` broadcast mentions
- Editing a message re-computing its mentions (4a has no edit; revisit with 4c)
