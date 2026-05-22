# WAAO — Desktop App (Electron)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** new `WaaoDesktop` (Electron shell), `WaaoFrontend` (small feature-detected hook)
- **Module:** Desktop distribution

## Goal

Ship WAAO as a desktop app for macOS + Windows: a thin Electron shell that loads the live hosted frontend, adds native OS notifications and a dock/taskbar unread badge, distributed unsigned via GitHub Releases.

## Locked decisions

| # | Decision |
|---|----------|
| Shell | Electron (JS/TS — same language as the team) |
| Content | The window loads the LIVE hosted frontend `https://waao-frontend.higorflopes.workers.dev` — nothing bundled. Frontend updates ship via Cloudflare as today; desktop users get them with zero desktop re-release |
| Platforms | macOS + Windows |
| Signing | Unsigned (no Apple/Windows certs). No clean auto-update — accepted for v1 |
| Native features | OS notifications for @mentions/DMs/meeting events + dock/taskbar unread badge |
| Repo | New dedicated `WaaoDesktop` repo |

## `WaaoDesktop` repo

```
WaaoDesktop/
  package.json            electron + electron-builder + typescript
  tsconfig.json
  src/main.ts             main process
  src/preload.ts          contextBridge — exposes window.waaoDesktop
  electron-builder.yml    build config
  build/icon.icns         macOS icon
  build/icon.ico          Windows icon
  .gitignore
  README.md               install + build instructions
```

### `src/main.ts` (main process)

- Creates a `BrowserWindow`: sensible default size (e.g. 1280×800), `minWidth: 940`, `minHeight: 600`, custom title "WAAO".
- Loads `APP_URL = "https://waao-frontend.higorflopes.workers.dev"`.
- `webPreferences`: `preload` set to the compiled preload, `contextIsolation: true`, `nodeIntegration: false` (security baseline).
- Window size/position persisted across launches (a small JSON in `app.getPath('userData')` — no extra dependency needed, or use `electron-window-state`).
- `webContents.setWindowOpenHandler` → external URLs (Jitsi pop-out, mailto, anything off the WAAO origin) open in the system browser via `shell.openExternal`, not a new Electron window.
- IPC handlers:
  - `desktop:setBadgeCount` (number) → `app.setBadgeCount(n)`
  - `desktop:showNotification` ({title, body}) → `new Notification({ title, body }).show()`; clicking the notification focuses the window.
- Standard lifecycle: quit when all windows closed (except macOS convention — stay in dock, re-create window on `activate`).
- Single-instance lock (`app.requestSingleInstanceLock()`) — a second launch focuses the existing window.

### `src/preload.ts`

- Via `contextBridge.exposeInMainWorld('waaoDesktop', { ... })`:
  - `isDesktop: true`
  - `setBadgeCount(n: number): void` → `ipcRenderer.send('desktop:setBadgeCount', n)`
  - `showNotification(payload: { title: string; body: string }): void` → `ipcRenderer.send('desktop:showNotification', payload)`
- Nothing else exposed — minimal attack surface.

### `electron-builder.yml`

- `appId: br.com.waao.desktop`, `productName: WAAO`
- macOS target `dmg` (x64 + arm64), Windows target `nsis` (x64)
- `mac.identity: null` + `win` with no cert → unsigned builds
- Output `dist/` — artifacts `WAAO-<version>-arm64.dmg`, `WAAO-<version>.dmg`, `WAAO Setup <version>.exe`

### Distribution

- `npm run dist` builds the installers locally.
- Installers uploaded to **GitHub Releases** of `WaaoDesktop`.
- README documents: macOS first-run = right-click → Open (Gatekeeper, unsigned); Windows = SmartScreen → More info → Run anyway.

## `WaaoFrontend` addition

A feature-detected hook — **no-op on web, active in Electron**:

`src/types/desktop.d.ts` — ambient typing:
```ts
interface WaaoDesktopApi {
  isDesktop: true;
  setBadgeCount(n: number): void;
  showNotification(p: { title: string; body: string }): void;
}
interface Window { waaoDesktop?: WaaoDesktopApi; }
```

`src/hooks/use-desktop-bridge.ts` — `useDesktopBridge()`:
- `setBadge(count: number)` — calls `window.waaoDesktop?.setBadgeCount(count)` (guarded).
- `notify(title, body)` — calls `window.waaoDesktop?.showNotification({ title, body })` (guarded).

Wired into `NotificationBell` (Phase 4b):
- On unread-count change → `setBadge(unreadCount)`.
- In the existing SignalR `onNotification` handler → `notify(notification.title, notification.body)`.

Both calls are guarded by `window.waaoDesktop` existing — on the web build they do nothing, no behavior change, no errors.

## Out of scope (v1)

- Code signing / notarization (→ unlocks real auto-update later, no rework)
- Auto-update (Squirrel needs signing); v1 README points users to Releases. Optional tiny "newer version available" banner deferred.
- System tray, auto-launch at login, global shortcuts, `waao://` deep-link protocol
- Bundling the frontend offline
- Linux build
- CI for the desktop builds (manual `npm run dist` for v1)

## Rollout

- `WaaoDesktop` — new repo, scaffolded + committed; the operator creates the GitHub repo and the first Release with built installers.
- `WaaoFrontend` — the hook is a normal frontend change: `git push origin main` → Cloudflare. Web users see zero difference; desktop users get badge + notifications.
- No backend change.
