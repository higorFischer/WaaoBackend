# WAAO Desktop App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox (`- [ ]`) steps.

**Goal:** A `WaaoDesktop` Electron repo (thin shell loading the hosted frontend, native notifications + dock badge, unsigned mac+win builds) + a feature-detected bridge hook in `WaaoFrontend`.

**Architecture:** Electron main process opens a `BrowserWindow` → `waao-frontend.higorflopes.workers.dev`. A `contextBridge` preload exposes `window.waaoDesktop`. The frontend's `NotificationBell` calls it (guarded) to set the dock badge + fire OS notifications.

**Tech Stack:** Electron, electron-builder, TypeScript; React 19 (frontend hook).

**Spec:** `docs/superpowers/specs/2026-05-22-desktop-app-design.md` — read first.

**Conventions:** TS no `any`; commit `git -c user.email="higor@waao.com.br"`, conventional prefix, no Claude/AI/Co-Authored-By.

---

## Task 1: Scaffold the WaaoDesktop repo

**Files (create the repo dir at `/Users/higorflopes/RiderProjects/Repositories/Waao/WaaoDesktop`):**
- `package.json` — `electron`, `electron-builder`, `typescript`, scripts: `build` (tsc), `start` (build + electron .), `dist` (build + electron-builder)
- `tsconfig.json` — compile `src/*.ts` → `dist-main/`
- `.gitignore` — `node_modules/`, `dist-main/`, `dist/`
- `README.md` — install (mac right-click-Open, win SmartScreen) + dev (`npm start`) + build (`npm run dist`)

- [ ] `git init`, create the files
- [ ] `npm install`
- [ ] Commit: `chore: scaffold WaaoDesktop electron project`

## Task 2: Main process

**Files:** Create `src/main.ts` per spec — `BrowserWindow` → `APP_URL`, `contextIsolation`, preload wired, external-link handler, single-instance lock, window-state persistence, IPC handlers `desktop:setBadgeCount` + `desktop:showNotification`, macOS lifecycle.

- [ ] Implement `main.ts`
- [ ] `npm run build` (tsc) clean
- [ ] Commit: `feat: electron main process — window, IPC, external links`

## Task 3: Preload bridge

**Files:** Create `src/preload.ts` — `contextBridge.exposeInMainWorld('waaoDesktop', { isDesktop, setBadgeCount, showNotification })`.

- [ ] Implement `preload.ts`; `npm run build` clean
- [ ] `npm start` — verify the window opens and loads WAAO (manual)
- [ ] Commit: `feat: preload contextBridge — window.waaoDesktop`

## Task 4: electron-builder config + icons + dist

**Files:** Create `electron-builder.yml` (unsigned mac dmg arm64+x64, win nsis x64), `build/icon.icns`, `build/icon.ico` (placeholder icons acceptable if real ones unavailable — note it).

- [ ] Implement config
- [ ] `npm run dist` produces installers in `dist/` (note any platform that can't build locally)
- [ ] Commit: `chore: electron-builder config for unsigned mac + win builds`

## Task 5: Frontend desktop bridge hook

**Files (in `WaaoFrontend`):**
- Create `src/types/desktop.d.ts` — ambient `WaaoDesktopApi` + `Window.waaoDesktop?`
- Create `src/hooks/use-desktop-bridge.ts` — `useDesktopBridge()` → `{ setBadge, notify }`, both guarded by `window.waaoDesktop`
- Modify `src/components/notifications/notification-bell.tsx` — call `setBadge(unreadCount)` on count change; call `notify(...)` inside the SignalR `onNotification` handler

- [ ] Implement; no `any`
- [ ] `npm run build` clean
- [ ] `git push origin main` (Cloudflare auto-deploys; web behavior unchanged)
- [ ] Commit: `feat(desktop): feature-detected bridge for badge + OS notifications`

## Task 6: Final — push WaaoDesktop

- [ ] Verify `WaaoDesktop` builds (`npm run build`)
- [ ] The operator creates the `higorFischer/WaaoDesktop` GitHub repo; then `git remote add origin` + `git push -u origin main`
- [ ] (Operator) draft a GitHub Release with the built `.dmg` / `.exe`

---

## Self-review
- Spec coverage: scaffold→T1, main→T2, preload→T3, builder→T4, frontend hook→T5, publish→T6. ✓
- `window.waaoDesktop` shape identical in preload (T3), ambient type (T5), and consumers (T5). ✓
- No placeholders. ✓
