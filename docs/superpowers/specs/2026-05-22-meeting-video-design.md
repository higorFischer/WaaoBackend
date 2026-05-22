# WAAO — Meeting Video (Phase 3 of the collaboration suite)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repo:** `WaaoFrontend` ONLY — no backend changes, no migration
- **Module:** Meetings (video extension)
- **Depends on:** Phase 2 Meetings (shipped)

## Goal

Give every meeting a "Join video" action that opens an embedded Jitsi call inside WAAO. Zero infrastructure, zero cost, zero API keys.

## Locked decisions

| # | Decision |
|---|----------|
| Embed | Embedded inside WAAO via the Jitsi **External API** (`external_api.js`) — full call UI in an in-app panel. |
| Jitsi host | Public `meet.jit.si`. One domain constant — swappable for a self-hosted instance later. |
| Room name | Derived client-side: `waao-<meetingId>` (the meeting GUID with dashes stripped). No backend storage. Recurring meetings share one room across occurrences. |
| Availability | "Join" is always available (no time-gating in v1). |
| Backend | None. Pure frontend. |

## Frontend changes (`WaaoFrontend`)

### `src/lib/jitsi.ts`
- `JITSI_DOMAIN = 'meet.jit.si'` constant.
- `roomNameForMeeting(meetingId: string): string` → `waao-${meetingId.replace(/-/g, '')}`.
- `loadJitsiScript(): Promise<void>` — injects `https://meet.jit.si/external_api.js` once (idempotent — resolves immediately if `window.JitsiMeetExternalAPI` already present), caches the load promise.

### `src/components/video/jitsi-call.tsx`
- Props: `{ meetingId: string; meetingTitle: string; displayName: string; onClose: () => void }`.
- Full-screen portal panel (`createPortal` to `document.body`, per the established pattern).
- On mount: `await loadJitsiScript()`, then `new window.JitsiMeetExternalAPI(JITSI_DOMAIN, { roomName, parentNode, userInfo: { displayName }, configOverwrite: { prejoinPageEnabled: true } })`.
- Listens for the Jitsi `readyToClose` event → calls `onClose`.
- On unmount: `api.dispose()` to tear down cleanly (no leaked iframes / media handles).
- A WAAO header bar above the iframe: meeting title + a "Leave" / close button (also calls `api.executeCommand('hangup')` then `onClose`).
- TypeScript: a minimal ambient declaration for `window.JitsiMeetExternalAPI` in `src/types/jitsi.d.ts` (no `any` — a typed `interface JitsiMeetExternalApi` with the members used: constructor, `dispose`, `addEventListener`, `executeCommand`).

### `src/pages/calendar/meeting-detail.tsx` (modify)
- Add a primary **"Join video"** button (lucide `Video` icon).
- Clicking it mounts `<JitsiCall>` with the meeting id/title and the current user's `fullName` as `displayName`.
- Visible to anyone who can see the meeting (organizer + attendees) — same audience as the panel itself.

### i18n
- `meetings.video.*` keys × 3 locales (pt-BR canonical, genuine en + es): join button, leave button, "connecting…", call header.

## Error / edge handling

- If `external_api.js` fails to load (offline / blocked), the `JitsiCall` panel shows a friendly error with a retry + a fallback link to open `https://meet.jit.si/<room>` in a new tab.
- `api.dispose()` on unmount and on close guarantees no orphaned call when navigating away.

## Out of scope

- Time-gating the Join button (always available in v1)
- Lobby / moderation / waiting room beyond Jitsi's built-in prejoin
- Recording, live captions
- Storing a custom room name per meeting (derived client-side; add a `Meeting.VideoRoomName` column later if customization is ever needed)
- Self-hosted Jitsi (swap `JITSI_DOMAIN` when/if desired)
- Presence ("who's in the call now") shown on the meeting card — possible fast-follow

## Rollout

- Frontend-only — `git push origin main`, Cloudflare auto-deploys. No backend deploy, no migration.
