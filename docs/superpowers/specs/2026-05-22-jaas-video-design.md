# WAAO — JaaS Video (Jitsi as a Service migration)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (JWT minting), `WaaoFrontend` (`JitsiCall` swap)
- **Module:** Meetings / Video
- **Supersedes:** the Phase 3 public-`meet.jit.si` embed

## Problem

`meet.jit.si` (the free public Jitsi server) now requires the meeting host to authenticate with an 8x8/Google account to *start* a room. An embedded app can't satisfy that — the host gets stuck at a login wall. Phase 3's "zero-setup public server" assumption no longer holds.

## Solution

Migrate to **JaaS (Jitsi as a Service)**. WAAO's backend becomes the identity provider: it signs a short-lived RS256 JWT per meeting room. The JWT carries the caller's identity and a **`moderator`** claim — `true` only for the meeting's organizer. JaaS trusts the JWT, so nobody ever sees an 8x8 login screen.

## JaaS account (provided)

| Item | Value |
|------|-------|
| AppID | `vpaas-magic-cookie-ac914149fa3944a8ab9410a8043c5df8` |
| Key ID (`kid`) | `vpaas-magic-cookie-ac914149fa3944a8ab9410a8043c5df8/4683d6` |
| Private key | RSA `.pem`, set as the Fly secret `Jaas__PrivateKey` (operator-set, never in source) |
| Domain | `8x8.vc` |

## Backend (`WaaoBackend`)

### Config

`JaasOptions` bound from configuration section `Jaas`:
- `AppId` (string)
- `KeyId` (string)
- `PrivateKey` (string — RSA private key PEM)

Fly secrets: `Jaas__AppId`, `Jaas__KeyId`, `Jaas__PrivateKey`. For local dev, `appsettings.Development.json` may hold non-secret `AppId`/`KeyId`; the private key stays out of source.

### `IJaasTokenService` / `JaasTokenService`

`string MintToken(JaasTokenRequest request)` — builds and RS256-signs a JaaS JWT.

JWT header: `{ alg: "RS256", kid: <KeyId>, typ: "JWT" }`.

JWT payload:
```
aud: "jitsi"
iss: "chat"
sub: <AppId>
room: <room name>            // the specific meeting room
iat / nbf: now
exp: now + 2 hours
context.user: {
  id:        <collaboratorId>,
  name:      <full name>,
  email:     <email>,
  avatar:    <photoUrl or empty>,
  moderator: <bool — true only for the meeting organizer>
}
context.features: {
  livestreaming: false, recording: false, transcription: false, "outbound-call": false
}
```

Signing: load the RSA key via `RSA.ImportFromPem(PrivateKey)`, wrap in `RsaSecurityKey`, sign with `SecurityAlgorithms.RsaSha256` using `Microsoft.IdentityModel.Tokens` / `System.IdentityModel.Tokens.Jwt` (already referenced by the existing auth `JwtIssuer`). The `RsaSecurityKey.KeyId` is set to the configured `KeyId` so the `kid` header is emitted.

### Endpoint

On `MeetingsController` (`[Authorize]`):
- `GET "{id:guid}/video-token"` → `MeetingVideoTokenDto { Token, AppId, Room }`
  - Load the meeting; caller must be the organizer or an attendee → else 403/404.
  - `moderator = (callerId == meeting.OrganizerId)`.
  - `room = roomNameForMeeting(meetingId)` — same derivation the frontend used: `waao-<meetingId-without-dashes>` (lowercase).
  - Returns the signed token + the AppId + the room name.

### `MeetingService`

Add `MeetingVideoTokenDto GetVideoTokenAsync(Guid meetingId, Guid callerId, ct)` — does the access check + delegates to `IJaasTokenService`.

### DI / Program.cs

`builder.Services.Configure<JaasOptions>(builder.Configuration.GetSection("Jaas"));`
`builder.Services.AddSingleton<IJaasTokenService, JaasTokenService>();`

## Frontend (`WaaoFrontend`)

### `src/lib/jitsi.ts`
- `JITSI_DOMAIN` → `'8x8.vc'`.
- `roomNameForMeeting` unchanged (`waao-<id>`), but the External API `roomName` becomes `${appId}/${room}` — the appId comes from the token endpoint response, not hard-coded.
- `loadJitsiScript()` → load `https://8x8.vc/<AppId>/external_api.js` (JaaS serves the External API under the AppID path). The AppID is needed before the script loads — so the script URL is built from the token response's `appId`.

### `src/services/meeting.service.ts`
- `getVideoToken(meetingId): Promise<{ token: string; appId: string; room: string }>` → `GET /meetings/{id}/video-token`.

### `src/components/video/jitsi-call.tsx`
- On mount: `await meetingService.getVideoToken(meetingId)` → `{ token, appId, room }`.
- `await loadJitsiScript(appId)` — loads `https://8x8.vc/<appId>/external_api.js`.
- `new JitsiMeetExternalAPI('8x8.vc', { roomName: \`${appId}/${room}\`, jwt: token, parentNode, userInfo, configOverwrite: { prejoinPageEnabled: true, disableDeepLinking: true } })`.
- Error state unchanged (retry only, no browser fallback).
- Props gain nothing — it already has `meetingId`; `displayName` still passed for `userInfo`.

## Error contract

| Case | HTTP |
|------|------|
| video-token for a meeting the caller doesn't organize/attend | 403 |
| video-token for an unknown meeting | 404 |
| JaaS config missing at runtime | 500 (logged) |

## Testing

- `JaasTokenService` unit test: minting with a test RSA keypair produces a JWT whose header `kid` = configured KeyId, `alg` = RS256, and whose payload has `room`, `sub` = AppId, and `context.user.moderator` matching the requested flag. (Use a generated test RSA key — not the real JaaS key.)
- `MeetingService.GetVideoTokenAsync`: organizer → `moderator: true`; attendee → `moderator: false`; non-member → throws (403).
- Frontend: manual smoke — organizer joins and is moderator; attendee joins as participant; no 8x8 login screen.

## Rollout

- New code only; no migration, no schema change.
- **Operator step:** set the three Fly secrets before/with deploy:
  - `fly secrets set Jaas__AppId="vpaas-magic-cookie-ac914149fa3944a8ab9410a8043c5df8" -a waao-api`
  - `fly secrets set Jaas__KeyId="vpaas-magic-cookie-ac914149fa3944a8ab9410a8043c5df8/4683d6" -a waao-api`
  - `fly secrets set Jaas__PrivateKey="$(cat /path/to/jaas-key.pem)" -a waao-api`
- Backend push → Fly CI/CD. Frontend push → Cloudflare.

## Out of scope

- Recording / livestreaming / transcription (JWT explicitly disables them)
- JaaS webhooks, usage analytics
- Self-hosted Jitsi (JaaS chosen instead)
