# Messaging Core (Phase 4a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship WAAO messaging core — public/private channels + 1:1 DMs, plain-text messages with SignalR live delivery, membership, unread tracking.

**Architecture:** New `Messaging` backend module (`Channel`, `ChannelMember`, `Message` entities). One migration `AddMessaging`. `ChannelService` + `MessageService`. SignalR `MessagingHub` at `/hubs/messaging`, JWT-authed. Frontend `/messages` two-pane app with `@microsoft/signalr` live updates.

**Tech Stack:** .NET 9, EF Core 9, SignalR, FluentValidation, React 19 + TS + Vite, `@microsoft/signalr`, TanStack Query.

**Spec:** `docs/superpowers/specs/2026-05-22-messaging-core-design.md` — read before starting.

**Golden examples:** Backend — `src/Waao.Services/Services/MeetingService.cs`, `src/Waao.API/Controllers/MeetingsController.cs`, migration `20260522154705_AddMeetings.cs`, `src/Waao.API/Program.cs`. Frontend — `src/pages/calendar/calendar-page.tsx`, `src/components/ui/portal-menu.tsx`, `src/services/meeting.service.ts`.

**Conventions (every task):** Backend TABS + file-scoped namespaces + primary-ctor DI (PascalCase); DTOs `record`; `Guid.CreateVersion7()`; `DateTime.UtcNow`; soft delete; enums `HasConversion<string>()`; FluentValidation. Frontend no `any`, `t()` × 3 locales, TanStack Query, WAAO UI lib, portal modals. Migrations via Bash heredoc (Write/Edit hook-blocked). Commit `git -c user.email="higor@waao.com.br"`, conventional prefix, no Claude/AI/Co-Authored-By. Push to `main`.

---

## Task 1: Entities + enums + EF configuration

**Files:**
- Create `src/Waao.Domain.Models/Enums/ChannelKind.cs` (`Public=0,Private=1,DirectMessage=2`) + `ChannelScope.cs` (`Custom=0,General=1,Department=2`)
- Create `src/Waao.Domain.Models/Entities/Messaging/Channel.cs`, `ChannelMember.cs`, `Message.cs` — fields per spec
- Create `src/Waao.Infra.EF/Configurations/ChannelConfiguration.cs`, `ChannelMemberConfiguration.cs`, `MessageConfiguration.cs` — snake_case, soft-delete filter, indexes per spec, enums `HasConversion<string>()`, unique `(ChannelId,CollaboratorId)`
- Modify `src/Waao.Infra.EF/WaaoDbContext.cs` — 3 DbSets + ApplyConfiguration

- [ ] Implement; `dotnet build src/Waao.API/Waao.API.csproj` passes
- [ ] Commit: `feat(messaging): domain entities + EF configuration`

## Task 2: Migration

- [ ] From `src/`: `dotnet ef migrations add AddMessaging -p Waao.Infra.EF -s Waao.API`
- [ ] Review SQL — additive; `messages.(channel_id, created_at)` index present
- [ ] Apply: `dotnet ef database update -p Waao.Infra.EF -s Waao.API` on `WaaoLocal`
- [ ] Verify `channels`, `channel_members`, `messages` tables with psql
- [ ] Commit: `feat(messaging): add migration AddMessaging`

## Task 3: DTOs + validators

**Files:**
- Create `src/Waao.Services.Abstractions/Dtos/Messaging/MessagingDtos.cs` — `ChannelDto`, `ChannelMemberDto`, `MessageDto`, `CreateChannelDto`, `PostMessageDto`, `MarkReadDto`, `MessagePageDto` (all `record`)
- Create `src/Waao.Services/Validation/Messaging/MessagingValidators.cs` — `CreateChannelValidator`, `PostMessageValidator`

- [ ] Implement; `dotnet build` passes
- [ ] Commit: `feat(messaging): DTOs + validators`

## Task 4: ChannelService

**Files:**
- Create `src/Waao.Services.Abstractions/Services/IChannelService.cs`
- Create `src/Waao.Services/Services/ChannelService.cs` — `ListMyChannelsAsync`, `ListPublicChannelsAsync`, `CreateChannelAsync`, `JoinAsync`, `LeaveAsync`, `AddMemberAsync`, `OpenDirectMessageAsync`, `MarkReadAsync`, `GetMembersAsync` per spec

- [ ] Write failing test: `OpenDirectMessageAsync` twice for the same pair returns the SAME channel id
- [ ] Run, confirm fails
- [ ] Implement `ChannelService`
- [ ] Add tests: create public/private + creator/initial members joined; public join ok; private join → 403; `AddMember` to private; `MarkRead` sets `LastReadMessageId`
- [ ] `dotnet test` green
- [ ] Commit: `feat(messaging): ChannelService`

## Task 5: MessageService

**Files:**
- Create `src/Waao.Services.Abstractions/Services/IMessageService.cs`
- Create `src/Waao.Services/Services/MessageService.cs` — `GetMessagesAsync` (membership check, `before` cursor, `limit` cap 100, `HasMore`), `PostMessageAsync` (membership check, persist, return `MessageDto`)

- [ ] Write failing test: post requires membership (non-member → throws); pagination returns `HasMore` correctly
- [ ] Run, confirm fails
- [ ] Implement `MessageService`; add the unread-count test (`ListMyChannelsAsync` counts messages after `LastReadMessageId`)
- [ ] `dotnet test` green
- [ ] Commit: `feat(messaging): MessageService with pagination + unread`

## Task 6: SignalR hub + controllers + DI + seed

**Files:**
- Create `src/Waao.API/Hubs/MessagingHub.cs` — `Hub`; on connect add the connection to `channel:{id}` groups for the caller's channels; `JoinChannelGroup`/`LeaveChannelGroup` methods
- Create `src/Waao.API/Controllers/ChannelsController.cs`, `DirectMessagesController.cs` — endpoints per spec; `PostMessage` persists then broadcasts `messageReceived` via `IHubContext<MessagingHub>`
- Modify `src/Waao.API/Program.cs` — `AddSignalR()`, register `IChannelService`/`IMessageService` (Scoped), `MapHub<MessagingHub>("/hubs/messaging")`, JWT `OnMessageReceived` to read `access_token` from query string for `/hubs` paths, verify CORS `AllowCredentials`
- Modify `src/Waao.Infra.EF/Seeds/DbInitializer.cs` — `SeedChannelsAsync` (#general + per-department, idempotent), call from `SeedAsync`
- Modify `src/Waao.Services/Services/AdminService.cs` `CreateDepartmentAsync` — create the department's channel

- [ ] Implement hub, controllers, DI, seed, department hook
- [ ] `dotnet build src/Waao.API/Waao.API.csproj` clean; `dotnet test` green
- [ ] `git push origin main`
- [ ] Commit: `feat(messaging): SignalR hub, controllers, DI, seed`

## Task 7: Frontend types + service + hub client

**Files:**
- Modify `WaaoFrontend/package.json` — add `@microsoft/signalr`; run `npm install`
- Create `WaaoFrontend/src/types/messaging.types.ts` — `Channel`, `ChannelMember`, `Message`, `ChannelKind`, `CreateChannelDto`, `MessagePage` (camelCase, no `any`)
- Create `WaaoFrontend/src/services/messaging.service.ts` — list/listPublic/create/join/leave/addMember/openDm/markRead/getMembers/getMessages/postMessage
- Create `WaaoFrontend/src/lib/messaging-hub.ts` — `HubConnectionBuilder` to `/hubs/messaging` with the JWT in `accessTokenFactory`, auto-reconnect, `onMessage(handler)` subscribe + `joinChannelGroup`/`leaveChannelGroup`

- [ ] Implement; commit: `feat(messaging): frontend types, service, SignalR hub client`

## Task 8: Messages page + channel list

**Files:**
- Create `WaaoFrontend/src/pages/messages/messages-page.tsx` — two-pane shell
- Create `WaaoFrontend/src/pages/messages/channel-list.tsx` — grouped Channels / DMs, unread badges, "+" menu (PortalMenu) for create-channel / start-DM, "Browse channels"
- Create `WaaoFrontend/src/pages/messages/create-channel-dialog.tsx` — portal modal: name, description, Public/Private, initial members
- Modify `WaaoFrontend/src/App.tsx` — `<Route path="/messages">`

- [ ] Implement; `npm run build` passes
- [ ] Commit: `feat(messaging): messages page + channel list + create dialog`

## Task 9: Conversation pane + composer + live updates

**Files:**
- Create `WaaoFrontend/src/pages/messages/conversation-pane.tsx` — message timeline (author grouping, auto-linkified text, infinite-scroll-up via `getMessages` `before` cursor, auto-scroll-down on new), header
- Create `WaaoFrontend/src/pages/messages/message-composer.tsx` — multiline, Enter sends / Shift+Enter newline
- Wire the SignalR hub in `messages-page.tsx` — connect on mount, append `messageReceived` to the open conversation, bump unread on others, `markRead` when a conversation is viewed/focused

- [ ] Implement; `npm run build` passes
- [ ] Commit: `feat(messaging): conversation pane, composer, live SignalR updates`

## Task 10: i18n + nav + ship

**Files:**
- Modify `WaaoFrontend/src/locales/{pt-BR,en,es}/common.json` — `messaging.*` namespace (channel list, create dialog, composer, empty states, browse). pt-BR canonical; genuine en + es.
- Modify `WaaoFrontend/src/components/layout/sidebar.tsx` — "Messages" nav item under the Work section

- [ ] Add i18n × 3 locales + nav
- [ ] `npm run build` clean; `dotnet build` clean
- [ ] `git push origin main` in both repos
- [ ] **Deploy note:** after the backend deploy, `waao-api` must run a single machine — `fly scale count 1 -a waao-api` (SignalR has no backplane). Flag this to the operator.
- [ ] Commit: `feat(messaging): i18n x3 locales + sidebar nav`

---

## Self-review
- Spec coverage: entities/enums→T1, migration→T2, DTOs/validators→T3, ChannelService→T4, MessageService→T5, hub/controllers/DI/seed→T6, frontend types/service/hub→T7, page/list→T8, conversation/live→T9, i18n/nav→T10. ✓
- Type consistency: `MessageDto`/`ChannelDto` defined T3, consumed T7 types + T8/T9; `messageReceived` payload = `MessageDto` both at the hub broadcast (T6) and the client handler (T7/T9). ✓
- No placeholders. ✓
