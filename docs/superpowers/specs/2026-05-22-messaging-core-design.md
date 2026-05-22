# WAAO — Messaging Core (Phase 4a of the collaboration suite)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Messaging

## Context

Phase 4 of the WAAO collaboration suite — an in-app Slack-style messaging layer. Phase 4 decomposes into three slices, each its own spec → plan → build:

| Slice | Scope | Status |
|-------|-------|--------|
| **4a** | **Messaging core + real-time** (this spec) | — |
| 4b | Mentions & notifications (`@person`, bell, unread surface) | future spec |
| 4c | Rich messages (attachments, reactions, threads, edit/delete UI) | future spec |

This document covers **4a only**.

## Goal

Channels (public + private) and 1:1 direct messages; post and read plain-text messages with live delivery via SignalR; channel membership; per-member unread tracking.

## Locked decisions

| # | Decision |
|---|----------|
| Conversation kinds | Public channels, Private channels, 1:1 Direct Messages |
| Channel creation | Any collaborator can create channels; plus an auto company-wide `#general` and one auto channel per Department |
| Message content | Plain text with line breaks; URLs auto-linkified on the client. No markdown/attachments/reactions in 4a |
| Real-time | SignalR hub; clients join a group per channel. REST is the source of truth; the socket is the live-delivery layer |
| Deployment | `waao-api` runs as a SINGLE Fly machine for 4a (SignalR has no backplane). A Redis backplane is the documented scale-out path |
| Unread | One `LastReadMessageId` per `ChannelMember` drives unread badges |

## Backend (`Waao.API` / `.Services` / `.Services.Abstractions` / `.Domain.Models` / `.Infra.EF`)

### Enums (`Waao.Domain.Models/Enums/`)

- `ChannelKind { Public=0, Private=1, DirectMessage=2 }`
- `ChannelScope { Custom=0, General=1, Department=2 }`

Both stored as string via `HasConversion<string>()`.

### Entities (`Waao.Domain.Models/Entities/Messaging/`)

`Channel`:
- `Id` (Guid v7), `Name` (≤120, nullable — DMs have no name), `Description` (≤500, nullable)
- `Kind` (ChannelKind), `Scope` (ChannelScope, default Custom), `DepartmentId` (Guid?, → Department — set for Department-scope channels)
- `CreatedById` (Guid → Collaborator)
- audit + soft-delete

`ChannelMember`:
- `Id` (Guid v7), `ChannelId` (FK), `CollaboratorId` (Guid → Collaborator)
- `LastReadMessageId` (Guid?, → Message — null = nothing read yet)
- `JoinedAt` (DateTime)
- audit + soft-delete; unique `(ChannelId, CollaboratorId)` where not deleted

`Message`:
- `Id` (Guid v7), `ChannelId` (FK), `AuthorId` (Guid → Collaborator)
- `Body` (≤4000)
- audit + soft-delete

### Migration

`AddMessaging` — additive. Indexes:
- `Channels.(Kind, IsDeleted)`, `Channels.DepartmentId`
- `ChannelMembers.(CollaboratorId, IsDeleted)` (powers "my channels"), `ChannelMembers.(ChannelId, IsDeleted)`
- `Messages.(ChannelId, CreatedAt)` — the history-pagination hot path

Applied locally to `WaaoLocal`; drift-checked.

### DTOs (`Waao.Services.Abstractions/Dtos/Messaging/`)

- `ChannelDto` — `{ Id, Name, Description, Kind, Scope, DepartmentId, MemberCount, IsMember, UnreadCount, LastMessagePreview?, LastMessageAtUtc?, OtherMember? }` (`OtherMember` = the other person for DMs)
- `ChannelMemberDto` — `{ CollaboratorId, CollaboratorName, CollaboratorPhotoUrl, JoinedAt }`
- `MessageDto` — `{ Id, ChannelId, AuthorId, AuthorName, AuthorPhotoUrl, Body, CreatedAtUtc }`
- `CreateChannelDto` — `{ Name, Description?, ChannelKind Kind, Guid[] InitialMemberIds }`
- `PostMessageDto` — `{ Body }`
- `MarkReadDto` — `{ Guid LastReadMessageId }`
- `MessagePageDto` — `{ Messages: MessageDto[], HasMore: bool }`

### Validators (FluentValidation, `Waao.Services/Validation/Messaging/`)

- `CreateChannelValidator` — Name NotEmpty ≤120 (required for Public/Private; ignored for DM which this endpoint never creates), Description ≤500, Kind must be Public or Private (DMs use the dedicated DM endpoint).
- `PostMessageValidator` — Body NotEmpty ≤4000.

### Services

`IChannelService`:
- `ListMyChannelsAsync(collaboratorId, ct)` — channels the caller is a member of (incl. DMs), each with `UnreadCount` + last-message preview, ordered by last activity.
- `ListPublicChannelsAsync(collaboratorId, ct)` — public channels the caller is NOT in (the "browse to join" list).
- `CreateChannelAsync(CreateChannelDto, creatorId, ct)` — creates a Public/Private channel; creator + `InitialMemberIds` become members; returns `ChannelDto`.
- `JoinAsync(channelId, collaboratorId, ct)` — public channels only (private → 403; must be added by a member).
- `LeaveAsync(channelId, collaboratorId, ct)`.
- `AddMemberAsync(channelId, collaboratorId, actorId, ct)` — actor must be a member; adds someone (used for private channels).
- `OpenDirectMessageAsync(otherCollaboratorId, callerId, ct)` — finds the existing 1:1 DM channel between the two, or creates it; returns `ChannelDto`.
- `MarkReadAsync(channelId, MarkReadDto, collaboratorId, ct)` — sets `LastReadMessageId`.
- `GetMembersAsync(channelId, callerId, ct)`.

`IMessageService`:
- `GetMessagesAsync(channelId, callerId, Guid? before, int limit, ct)` — caller must be a member (else 403/404). Returns `MessagePageDto` — `limit` messages (cap 100) ending before the `before` cursor (null = newest), newest-last; `HasMore` indicates older messages exist.
- `PostMessageAsync(channelId, PostMessageDto, authorId, ct)` — caller must be a member; persists; returns `MessageDto`; the controller then broadcasts it over SignalR.

### SignalR

- `MessagingHub : Hub` at `/hubs/messaging`. Authenticated with the same JWT bearer (configure SignalR to read the token from the `access_token` query string, the standard pattern for WebSockets).
- On connect: the hub adds the connection to a SignalR group `channel:{channelId}` for every channel the caller is a member of.
- Server → client event `messageReceived(MessageDto)`. The controller's `PostMessage` action, after persisting, calls `IHubContext<MessagingHub>` to broadcast to `channel:{channelId}`.
- Hub methods `JoinChannelGroup(channelId)` / `LeaveChannelGroup(channelId)` so a freshly created/joined channel starts receiving live messages without a reconnect.
- REST remains the source of truth — if the socket is down, posting still works via REST and the sender sees their message immediately; other clients catch up on next fetch.

### Controllers

`ChannelsController` `[Route("api/waao/channels")] [Authorize]`:
- `GET ""` — my channels; `GET "public"` — joinable public channels
- `POST ""` — create; `POST "{id:guid}/join"`, `POST "{id:guid}/leave"`
- `POST "{id:guid}/members"` — add member; `GET "{id:guid}/members"`
- `POST "{id:guid}/read"` — mark read
- `GET "{id:guid}/messages"` — `[FromQuery] before, limit`
- `POST "{id:guid}/messages"` — post (then broadcast via `IHubContext`)

`DirectMessagesController` `[Route("api/waao/dm")] [Authorize]`:
- `POST "{collaboratorId:guid}"` — open-or-create DM → `ChannelDto`

One-line expression-bodied where possible (the post-message action is a small block: persist then broadcast).

### DI / Program.cs

- `builder.Services.AddSignalR();`
- Register `IChannelService`, `IMessageService` (Scoped).
- `app.MapHub<MessagingHub>("/hubs/messaging");`
- JWT bearer: add the `OnMessageReceived` event to pull `access_token` from the query string for `/hubs` paths.
- CORS: the existing `Cors:AllowedOrigins` policy must allow credentials for the socket (`AllowCredentials` — verify it's already set; SignalR needs it).

### Seed

`DbInitializer.SeedChannelsAsync` (idempotent): creates `#general` (Scope=General, all collaborators as members) + one Department-scope channel per Department (department members joined). A hook on `AdminService.CreateDepartmentAsync` creates the channel for a newly added department.

## Frontend (`WaaoFrontend`)

- `@microsoft/signalr` added to `package.json`.
- New route `/messages` — two-pane layout inside `AuthedShell`.
- `src/types/messaging.types.ts`, `src/services/messaging.service.ts`.
- `src/lib/messaging-hub.ts` — wraps `HubConnectionBuilder`, connects to `/hubs/messaging` with the JWT, auto-reconnect, exposes subscribe/unsubscribe for `messageReceived`.
- Components:
  - `MessagesPage` — left channel rail + right conversation pane.
  - `ChannelList` — grouped Channels / Direct Messages, unread badges, "+" to create channel / start DM, "Browse channels" to join public ones.
  - `CreateChannelDialog` — portal modal: name, description, Public/Private, initial members picker.
  - `StartDmDialog` / inline picker — pick a collaborator → `OpenDirectMessage`.
  - `ConversationPane` — message timeline (grouped by author, auto-linkified text, infinite-scroll-up for history, auto-scroll-down on new), `MessageComposer` (multiline, Enter sends / Shift+Enter newline).
- Live: on mount connect the hub; append `messageReceived` payloads to the open conversation + bump unread on others; `markRead` when a conversation is viewed.
- Sidebar nav item "Mensagens / Messages / Mensajes"; i18n `messaging.*` × 3 locales (pt-BR canonical, genuine en + es).
- Reuse the portal modal pattern + `PortalMenu`.

## Error contract

| Case | HTTP |
|------|------|
| Create channel invalid (no name, bad kind) | 400 |
| Join a private channel | 403 |
| Read/post/list-messages on a channel you're not in | 403 |
| Unknown channel / message cursor | 404 |
| Post empty / >4000 chars | 400 |

## Testing (`Waao.Tests`, EF InMemory)

- Create channel: creator + initial members joined; Public vs Private.
- Join: public join works; private join → 403; AddMember to private works for a member.
- DM: `OpenDirectMessageAsync` creates a 2-member DM; calling again returns the SAME channel; DM never duplicated.
- Messages: post requires membership (non-member → 403); `GetMessagesAsync` pagination — `before` cursor + `limit` cap + `HasMore` flag.
- Unread: `MarkReadAsync` sets `LastReadMessageId`; `UnreadCount` in `ListMyChannelsAsync` counts messages after it.
- Seed: `#general` + per-department channels idempotent.
- (SignalR hub broadcast logic is thin; covered by manual smoke — not unit-tested.)

## Rollout / safety

- New tables only — additive migration, no destructive ops, no backfill. Applied under the startup advisory lock.
- **Deploy step: scale `waao-api` to a single machine** (`fly scale count 1`) before/with this release — SignalR has no backplane. Document the Redis-backplane path for future multi-instance.
- Frontend push → Cloudflare. Backend push → Fly CI/CD.
- Verify the CORS policy sends `Access-Control-Allow-Credentials` (SignalR WebSocket needs it).

## Out of scope (4a — deferred)

- `@mentions` + notifications/bell/unread-surface — **4b**
- Attachments, reactions, threads, message edit/delete UI — **4c**
- Group DMs (3+ people), message search, typing indicators, presence/online status, read receipts per message
- Redis backplane / multi-instance SignalR (single machine for now)
