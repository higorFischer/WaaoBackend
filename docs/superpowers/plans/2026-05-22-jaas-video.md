# JaaS Video Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox steps.

**Goal:** Migrate WAAO video from public meet.jit.si to JaaS — backend mints a signed RS256 JWT per meeting (moderator = organizer); frontend `JitsiCall` uses `8x8.vc` + the JWT.

**Spec:** `docs/superpowers/specs/2026-05-22-jaas-video-design.md` — read first.

**Conventions:** Backend TABS, file-scoped namespaces, primary-ctor DI; `record` DTOs; `DateTime.UtcNow`. Frontend no `any`, `t()`, TanStack Query. Commit `git -c user.email="higor@waao.com.br"`, conventional prefix, no Claude/AI/Co-Authored-By. Push to `main`.

---

## Task 1: Backend — JaaS options + token service

**Files:**
- Create `src/Waao.Services/Video/JaasOptions.cs` — `record JaasOptions { AppId, KeyId, PrivateKey }`
- Create `src/Waao.Services.Abstractions/Services/IJaasTokenService.cs` + `Dtos/JaasTokenRequest.cs` (`{ Guid CollaboratorId, string Name, string Email, string? Avatar, string Room, bool Moderator }`)
- Create `src/Waao.Services/Video/JaasTokenService.cs` — builds + RS256-signs the JaaS JWT per spec. Use `RSA.ImportFromPem`, `RsaSecurityKey` (set `.KeyId`), `SigningCredentials(key, SecurityAlgorithms.RsaSha256)`, `JwtSecurityTokenHandler`. Payload claims exactly per spec (aud/iss/sub/room/iat/nbf/exp + `context` object with `user` + `features`).

- [ ] Write a failing test `JaasTokenServiceTests` — generate a throwaway RSA key, mint a token, assert header `kid` + `alg=RS256` + payload `room`/`sub`/`context.user.moderator`
- [ ] Run, confirm fails
- [ ] Implement `JaasTokenService`
- [ ] Test passes; `dotnet build src/Waao.API/Waao.API.csproj` clean
- [ ] Commit: `feat(video): JaaS JWT token service`

## Task 2: Backend — service method + endpoint + DI

**Files:**
- Create `src/Waao.Services.Abstractions/Dtos/Meetings/MeetingVideoTokenDto.cs` (`record { string Token, string AppId, string Room }`)
- Modify `IMeetingService` + `MeetingService` — add `GetVideoTokenAsync(Guid meetingId, Guid callerId, ct)`: load meeting, caller must be organizer or attendee (else KeyNotFound/Unauthorized → 404/403), `moderator = callerId == OrganizerId`, room = `waao-` + meetingId without dashes (lowercase), call `IJaasTokenService`.
- Modify `MeetingsController` — `GET "{id:guid}/video-token"` → `MeetingVideoTokenDto`.
- Modify `Program.cs` — `Configure<JaasOptions>(config.GetSection("Jaas"))`, `AddSingleton<IJaasTokenService, JaasTokenService>()`.

- [ ] Write failing test: organizer → `moderator true`; attendee → `moderator false`; non-member → throws
- [ ] Run, confirm fails
- [ ] Implement
- [ ] `dotnet build` clean, `dotnet test` green
- [ ] `git push origin main`
- [ ] Commit: `feat(video): meeting video-token endpoint`

## Task 3: Frontend — service + jitsi lib

**Files:**
- Modify `WaaoFrontend/src/lib/jitsi.ts` — `JITSI_DOMAIN = '8x8.vc'`; `loadJitsiScript(appId: string)` loads `https://8x8.vc/${appId}/external_api.js` (idempotent/cached as before); keep `roomNameForMeeting`.
- Modify `WaaoFrontend/src/services/meeting.service.ts` — `getVideoToken(meetingId): Promise<{ token: string; appId: string; room: string }>`.

- [ ] Implement; `npm run build` clean
- [ ] Commit: `feat(video): frontend JaaS token service + 8x8.vc lib`

## Task 4: Frontend — JitsiCall uses JaaS

**Files:** Modify `WaaoFrontend/src/components/video/jitsi-call.tsx`:
- On mount: `getVideoToken(meetingId)` → `{ token, appId, room }`; then `loadJitsiScript(appId)`; then `new Api('8x8.vc', { roomName: \`${appId}/${room}\`, jwt: token, parentNode, userInfo: { displayName }, configOverwrite: { prejoinPageEnabled: true, disableDeepLinking: true } })`.
- Error phase if the token fetch or script load fails — retry only (no browser fallback — unchanged).

- [ ] Implement; `npm run build` clean
- [ ] `git push origin main`
- [ ] Commit: `feat(video): JitsiCall connects via JaaS with a signed JWT`

---

## Self-review
- Spec coverage: options/token-service→T1, endpoint/service/DI→T2, frontend lib/service→T3, JitsiCall→T4. ✓
- `MeetingVideoTokenDto { Token, AppId, Room }` shape identical backend (T2) ↔ frontend (T3 service return). ✓
- Room derivation (`waao-<id>`) identical to the prior `roomNameForMeeting`. ✓
