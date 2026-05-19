# WAAO — Manual XP economy + clean slate + single admin

- **Date:** 2026-05-19
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Gamification / Auth / Admin
- **Sequence:** Feature **B** of B → C → A. B redefines the gamification model
  that the email-verification spec (C) depends on; C's spec is revised after this.

## Goal

Invert the gamification model. Today every career event, badge unlock and
login/activity streak automatically awards XP and drives level-ups. After this
change, **no site action awards XP automatically**. XP is granted **only** by an
admin, manually, with an amount and a reason. Every user starts at **level 0**
with **0 XP**, and all existing XP/levels/history are reset. Only
`higor@waao.com.br` exists as a user (Admin); previously-seeded users are removed.

## Locked decisions (from brainstorming)

| Topic | Decision |
|-------|----------|
| Existing data | Reset all: `TotalXp=0`, `CurrentLevel=0`, **clear** `xp_transactions`. Destructive, intentional. |
| Auto badges/streaks | **Keep** auto badge unlocks and auto streak tracking — they award **0 XP**. |
| Admin grant | Generic manual grant: amount + reason, per collaborator. |
| Users | Only `higor@waao.com.br` (Admin) remains; other users soft-deleted. |
| 1 — Level model | Clamp `CurrentLevel` to 0 below the first `LevelDefinition` threshold; **no** Level-0 seed row. |
| 2 — Badge XpReward | Keep `XpReward` values in DB (display/future), simply don't award. |
| 3 — xp_transactions reset | **Hard `DELETE`** the ledger (true clean slate), not soft-delete. |
| 4 — Admin grant amount | Allow **negative** amounts (corrections/deductions); reject 0. |
| 5 — Bootstrap user | Seed `higor@waao.com.br`: Admin, `EmailVerified=true`, password `Waao2026!` (bootstrap — rotate after). |
| 6 — Celebrations | Remove auto `xp-chip` / `level-up-overlay` (nothing auto-grants XP). Badge-unlock toast stays. |

## Level-0 model

- `Collaborator.CurrentLevel` default → `0` (was `1`).
- `GamificationEngine.ComputeLevelAsync(totalXp)`: if `totalXp` < the lowest
  `LevelDefinition.XpThreshold` (or no definitions), return `0`. Otherwise the
  highest definition whose threshold ≤ `totalXp` (unchanged logic above 0).
- `LevelDefinitions` (1→20, seeded) are unchanged; reachable only via admin grants.

## Backend changes

### `GamificationEngine`
- `RecordAsync` remains the single XP write path (XpTransaction + `TotalXp` +
  `ComputeLevelAsync`). Now invoked **only** by the admin grant.
- `AwardCareerEventXpAsync` becomes dead code — **remove the method** (and its
  `XpRules.XpForCareerEvent` usage if it has no other callers) rather than leave
  unused. We're editing this class anyway; no orphaned XP paths left behind.

### `CareerEventService`
- Career events still created and recorded. **Remove** the
  `GamificationEngine.AwardCareerEventXpAsync` call. `CareerEvent.XpAwarded = 0`.
  `LevelBefore == LevelAfter` (no change). Badges still evaluated (0 XP).

### `BadgeEvaluator`
- Badge unlock logic unchanged (collaborators still earn badges on milestones).
- **Remove** the `if (badge.XpReward > 0) … RecordAsync(…, XpSource.BadgeUnlock …)`
  block. `Badge.XpReward` column/data kept (display only).

### `StreakTracker`
- Streak counters (`CurrentStreakDays`, login streak, etc.) still updated.
- **Remove** the streak bonus-XP `RecordAsync` path. Return `bonusAwarded = 0`.
- Callers (`AuthService` login/register) compile unchanged; they simply observe 0.

### `AuthService`
- No code change required for XP (it flows through Streaks/Badges which no longer
  award). `BuildResult` still returns `LoginStreakDays` (real) and
  `LoginStreakBonusXp = 0`, `NewBadges` (real). Confirm no direct XP writes.

### Admin manual grant
- DTO: `record GrantXpDto { int Amount; string Reason }` (Amount ≠ 0; Reason
  non-empty, ≤ 280). FluentValidation `GrantXpValidator`.
- `IAdminService.GrantXpAsync(Guid collaboratorId, GrantXpDto dto, Guid adminId, CancellationToken ct) : Task<CollaboratorDto>`
  → loads collaborator (404 if missing/deleted) → `GamificationEngine.RecordAsync(
  collaboratorId, dto.Amount, XpSource.Admin, dto.Reason, sourceEntityId: adminId,
  sourceEntityType: "AdminGrant", ct)` → `SaveChanges` → return mapped dto with
  new `TotalXp`/`CurrentLevel`. (`XpSource.Admin = 99` already exists.)
- `AdminController` (already `[Authorize(Policy="Admin")]`):
  `POST collaborators/{id:guid}/grant-xp` → `Ok(await Service.GrantXpAsync(id, dto, Me, ct))`,
  `[ProducesResponseType(typeof(CollaboratorDto),200)]`, `404`, `400`.

### Migration `ManualXpEconomyReset`
1. Schema: `collaborators.current_level` default `0` (was `1`).
2. Data (raw SQL in migration `Up`, idempotent):
   - `UPDATE collaborators SET total_xp = 0, current_level = 0;`
   - `DELETE FROM xp_transactions;` (ledger hard-cleared — clean slate).
     **Deliberate exception to the MANDATORY "soft delete only" rule**: this is a
     gamification ledger reset explicitly chosen by the user (decision #3). It is
     the *only* hard-delete; collaborators are still **soft**-deleted (next step).
   - `UPDATE collaborators SET is_deleted = true, updated_at = now()
      WHERE lower(email) <> 'higor@waao.com.br' AND is_deleted = false;`
   - Soft-delete cascade not required (queries already filter `!IsDeleted`).
3. `Down`: irreversible data step — document as non-reversible (no attempt to
   restore deleted users / XP). Schema default revert only.
4. Applied to local `WaaoLocal` first; reviewed before `database update`.

### `DbInitializer`
- Replace `SeedDefaultAdminsAsync` user list with a single user:
  `higor@waao.com.br`, `FullName "Higor"`, `RoleKind = Admin`,
  `EmailVerified = true`, `EmailVerifiedAt = UtcNow`, `JoinDate`, password
  `Waao2026!` (bootstrap). Guarded by the existing per-email
  `if (await db.Collaborators.AnyAsync(c => c.Email == email)) continue;`.
- Levels / badges / departments seeds unchanged. Seeded users created post-change
  start at `TotalXp=0`, `CurrentLevel=0` (entity defaults).

## Frontend changes (`WaaoFrontend`, own UI lib)

- **Admin grant UI**: in `admin-panel.tsx` / collaborator detail, a "Grant XP"
  control — number input (allows negative) + reason text → calls
  `adminService.grantXp(collaboratorId, { amount, reason })`; on success refresh
  the collaborator (new XP/level). `admin.service.ts` gains `grantXp`.
- **Remove auto-celebrations** driven by automatic XP: stop rendering `xp-chip`
  and `level-up-overlay` on login and career-event responses. `badge-unlock-toast`
  stays (badges still unlock). Remove now-dead XP/level-up wiring from the login
  and career-event flows (they will always be 0 / unchanged).
- **Display**: dashboard, leaderboard, `level-ring` keep rendering `TotalXp` /
  `CurrentLevel`; after reset these show `0` for everyone until an admin grants —
  no logic bug, copy reviewed so "Level 0 / 0 XP" reads sensibly.
- **i18n**: new admin strings (`admin.grantXp.*`) added to **all three**
  `src/locales/{pt-BR,en,es}/common.json`, pt-BR authored as source (aligns with
  Feature A's pt-BR-source decision).

## Interaction with Feature C (email verification)

After this spec is approved, the email-verification spec
(`2026-05-19-email-verification-design.md`) is revised:
- Drop "day-one badges/streak/**XP** fire on verify/login" — under B there is no
  XP; badges/streak still run (0 XP) via existing login/verify paths.
- Note `higor@waao.com.br` is the seeded **verified** bootstrap Admin (consistent
  with C's `Auth:AdminEmails` default and verification grandfathering).
- C's migration backfill (`email_verified = true` for existing rows) still applies;
  with B, the only existing row is `higor@waao.com.br` (already verified by seed).

## Error contract

| Case | HTTP | Body |
|------|------|------|
| Grant XP, not admin | 403 | (auth policy) |
| Grant XP, collaborator missing/deleted | 404 | not found |
| Grant XP, Amount 0 or empty reason | 400 | FluentValidation failure |
| Grant XP, success | 200 | updated `CollaboratorDto` |

## Testing

- **Backend unit tests** (test Postgres / SQLite, gamification deps real):
  - career event: `XpAwarded == 0`, level unchanged, badges still evaluated
  - badge unlock: badge granted, **no** `XpTransaction`, `TotalXp` unchanged
  - streak: counters advance, `bonusAwarded == 0`, no `XpTransaction`
  - admin grant: positive adds XP + recomputes level + writes
    `XpTransaction{Source=Admin}`; negative deducts; `Amount==0` → validation error;
    non-admin → 403; missing collaborator → 404
  - `ComputeLevelAsync`: 0 when below first threshold; correct above
  - migration: balances zeroed, `xp_transactions` empty, only `higor@waao.com.br`
    not soft-deleted, `current_level` default is 0
- **Migration** applied to local `WaaoLocal` and inspected before deploy.
- **Frontend**: manual smoke — admin grants +/- XP and sees level/XP update;
  login shows no XP/level-up celebration; badge unlock toast still appears.

## Rollout / safety

- **Destructive** by design (XP/level reset, ledger cleared, users soft-deleted).
  Production currently holds only seeded data (deployed 2026-05-19, no real
  signups), so blast radius is the seed set — acceptable and intended.
- Order: backend (migration + API) deployed via `fly deploy`; frontend via
  `git push` (Cloudflare Worker CI/CD). Migration runs on startup behind the
  Postgres advisory lock already in place.
- After deploy, `higor@waao.com.br` / `Waao2026!` is the only login — rotate the
  password promptly (and/or rely on Feature C once shipped).
