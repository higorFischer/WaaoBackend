# Mentions & Notifications (Phase 4b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship `@mention` in channel messages + a bell/notification surface fed by messaging & meeting events, delivered live over the existing SignalR hub.

**Architecture:** New `Notification` + `MessageMention` entities, migration `AddNotifications`. `NotificationService` (create + broadcast + list + mark-read), called by `MessageService`/`ChannelService`/`MeetingService`. `MessagingHub` extended with a per-user group. Frontend: bell + panel, composer `@` autocomplete, timeline mention chips.

**Tech Stack:** .NET 9, EF Core 9, SignalR, FluentValidation, React 19 + TS + Vite, `@microsoft/signalr`, TanStack Query.

**Spec:** `docs/superpowers/specs/2026-05-22-mentions-notifications-design.md` — read before starting.

**Golden examples:** Backend — `src/Waao.Services/Services/MessageService.cs`, `ChannelService.cs`, `MeetingService.cs`, `src/Waao.API/Hubs/MessagingHub.cs`, `src/Waao.API/Controllers/ChannelsController.cs`, migration `20260522*_AddMessaging.cs`. Frontend — `src/lib/messaging-hub.ts`, `src/pages/messages/message-composer.tsx`, `conversation-pane.tsx`, `src/components/ui/portal-menu.tsx`, `src/components/layout/sidebar.tsx`.

**Conventions:** Backend TABS, file-scoped namespaces, primary-ctor DI PascalCase, DTOs `record`, `Guid.CreateVersion7()`, `DateTime.UtcNow`, soft delete, enums `HasConversion<string>()`. Frontend no `any`, `t()` × 3 locales, TanStack Query, WAAO UI lib, portal modals. Migrations via Bash heredoc (Write/Edit hook-blocked). Commit `git -c user.email="higor@waao.com.br"`, conventional prefix, no Claude/AI/Co-Authored-By. Push to `main`.

---

## Task 1: Entities + enum + EF config + migration

**Files:**
- Create `src/Waao.Domain.Models/Enums/NotificationKind.cs`
- Create `src/Waao.Domain.Models/Entities/Notifications/Notification.cs`; `src/Waao.Domain.Models/Entities/Messaging/MessageMention.cs`
- Create `src/Waao.Infra.EF/Configurations/NotificationConfiguration.cs`, `MessageMentionConfiguration.cs` — snake_case, soft-delete filter, indexes per spec, enum `HasConversion<string>()`, unique `(MessageId,MentionedCollaboratorId)`
- Modify `WaaoDbContext.cs` — 2 DbSets + ApplyConfiguration
- Migration `AddNotifications` via `dotnet ef migrations add` then author the .cs via Bash heredoc; apply to `WaaoLocal`

- [ ] Implement entities/enum/config/DbContext; `dotnet build src/Waao.API/Waao.API.csproj` passes
- [ ] Create + apply migration
- [ ] Commit: `feat(notifications): entities, EF config, migration`

## Task 2: DTOs + MentionParser

**Files:**
- Create `src/Waao.Services.Abstractions/Dtos/Notifications/NotificationDtos.cs` — `NotificationDto`, `NotificationListDto`, `MessageMentionDto` (all `record`)
- Modify `src/Waao.Services.Abstractions/Dtos/Messaging/MessagingDtos.cs` — add `Mentions: MessageMentionDto[]` to `MessageDto`
- Create `src/Waao.Services/Messaging/MentionParser.cs` — static `ExtractCollaboratorIds(string body): IReadOnlyList<Guid>` with the regex per spec

- [ ] Write failing test `MentionParserTests` — extracts ids from `@[Ana](guid)`, ignores malformed, de-dupes
- [ ] Run, confirm fails
- [ ] Implement DTOs + MentionParser
- [ ] Test passes; `dotnet build` passes
- [ ] Commit: `feat(notifications): DTOs + mention parser`

## Task 3: NotificationService

**Files:**
- Create `src/Waao.Services.Abstractions/Services/INotificationService.cs`
- Create `src/Waao.Services/Services/NotificationService.cs` — `CreateAsync` (persist; broadcast `notificationReceived` to `user:{recipientId}` via `IHubContext<MessagingHub>`; no-op when `recipientId == actorId`), `ListAsync`, `MarkReadAsync`, `MarkAllReadAsync`. Inject `IHubContext<MessagingHub>`.

- [ ] Write failing test: `CreateAsync` persists a row; `CreateAsync` with `recipientId == actorId` is a no-op; `MarkRead` of another user's notification throws
- [ ] Run, confirm fails
- [ ] Implement `NotificationService`
- [ ] Tests pass (`ListAsync` unreadOnly + `UnreadCount`, `MarkAllRead`)
- [ ] `dotnet test` green
- [ ] Commit: `feat(notifications): NotificationService`

## Task 4: Wire mention + event sources

**Files:**
- Modify `src/Waao.Services/Services/MessageService.cs` `PostMessageAsync` — parse mentions, keep channel-members-not-author, write `MessageMention` rows + `Mention` notifications; include `Mentions` in the returned `MessageDto`
- Modify `src/Waao.Services/Services/ChannelService.cs` `AddMemberAsync` — `ChannelInvite` notification
- Modify `src/Waao.Services/Services/MeetingService.cs` — `CreateAsync` → `MeetingInvite`; `UpdateAsync` → `MeetingUpdated`; `CancelAsync` → `MeetingCancelled` (attendees minus actor)
- Inject `INotificationService` into those three services

- [ ] Write failing tests: mention of a channel member → notification; mention of a non-member → none; self-mention → none; `AddMember` → `ChannelInvite`; meeting create/update/cancel → right kinds for attendees, never the actor
- [ ] Run, confirm fail
- [ ] Implement the wiring
- [ ] `dotnet test` green
- [ ] Commit: `feat(notifications): wire mentions, channel-invite, meeting events`

## Task 5: Hub per-user group + controller + DI

**Files:**
- Modify `src/Waao.API/Hubs/MessagingHub.cs` `OnConnectedAsync` — also add the connection to `user:{collaboratorId}`
- Create `src/Waao.API/Controllers/NotificationsController.cs` — endpoints per spec
- Modify `src/Waao.API/Program.cs` — register `INotificationService` (Scoped)

- [ ] Implement; `dotnet build src/Waao.API/Waao.API.csproj` clean; `dotnet test` green
- [ ] `git push origin main`
- [ ] Commit: `feat(notifications): hub per-user group, controller, DI`

## Task 6: Frontend types + service + hub event

**Files:**
- Create `WaaoFrontend/src/types/notification.types.ts`
- Modify `WaaoFrontend/src/types/messaging.types.ts` — add `mentions` to `Message`
- Create `WaaoFrontend/src/services/notification.service.ts` — list / unreadCount / markRead / markAllRead
- Modify `WaaoFrontend/src/lib/messaging-hub.ts` — `onNotification(handler)` subscribe to `notificationReceived`

- [ ] Implement; commit: `feat(notifications): frontend types, service, hub event`

## Task 7: Bell + notification panel

**Files:**
- Create `WaaoFrontend/src/components/notifications/notification-bell.tsx` — bell + unread badge, in the sidebar by the user-profile block
- Create `WaaoFrontend/src/components/notifications/notification-panel.tsx` — portal dropdown list; row click marks read + navigates (`channel` → `/messages`, `meeting` → `/calendar`); "mark all read"
- Modify `WaaoFrontend/src/components/layout/sidebar.tsx` — mount `<NotificationBell />`
- Live: subscribe to `onNotification` — bump badge + prepend

- [ ] Implement; `npm run build` passes
- [ ] Commit: `feat(notifications): bell + notification panel`

## Task 8: Composer @mention + timeline chips + i18n

**Files:**
- Modify `WaaoFrontend/src/pages/messages/message-composer.tsx` — typing `@` opens an inline channel-member autocomplete; selecting inserts `@[Name](id)`; backspace removes a token cleanly
- Modify `WaaoFrontend/src/pages/messages/conversation-pane.tsx` — render `@[Name](id)` tokens as highlighted mention chips (current-user chip gets an accent), using `message.mentions` for names
- Modify `WaaoFrontend/src/locales/{pt-BR,en,es}/common.json` — `notifications.*` namespace (bell, panel, mark-all-read, mention picker, empty states). pt-BR canonical; genuine en + es.

- [ ] Implement; `npm run build` clean; `dotnet build` clean
- [ ] `git push origin main` in both repos
- [ ] Commit: `feat(notifications): composer mentions, timeline chips, i18n x3`

---

## Self-review
- Spec coverage: entities/migration→T1, DTOs/parser→T2, NotificationService→T3, event wiring→T4, hub/controller/DI→T5, frontend types/service/hub→T6, bell/panel→T7, composer/chips/i18n→T8. ✓
- Type consistency: `NotificationDto` (T2) = `notificationReceived` payload (T3 broadcast, T6 handler); `MessageDto.Mentions` (T2) consumed by T8 chips. ✓
- No placeholders. ✓
