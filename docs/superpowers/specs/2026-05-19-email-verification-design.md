# WAAO — `@waao.com.br` self-registration + email verification

- **Date:** 2026-05-19
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Auth

## Goal

Anyone with an `@waao.com.br` email can self-register. The account is created
**unverified** and cannot log in until the email is verified via a tokenized link
sent through Resend.com. `higor@waao.com.br` (configurable list) is assigned the
`Admin` role on creation; every other `@waao.com.br` registrant is `Collaborator`.
A rate-limited "resend verification email" path exists.

## Non-goals

- SSO / external identity providers (explicitly rejected during brainstorming).
- Password reset / "forgot password" (out of scope for this feature).
- Changing the existing seeded `@waao.com` accounts' behaviour beyond
  grandfathering them as already-verified.

## Flow

1. `POST /auth/register` — email must end `@waao.com.br` (validator). Creates a
   `Collaborator` with `EmailVerified=false`, an opaque 32-byte base64url token
   and a 24h expiry. Role assigned from `Auth:AdminEmails`. Sends the verification
   email. Returns `{ status: "verification_sent", email }` — **no JWT**.
2. User opens `<Frontend:BaseUrl>/verify-email?token=…`. Frontend calls
   `POST /auth/verify-email { token }`. Service validates token + expiry, sets
   `EmailVerified=true`, `EmailVerifiedAt=now`, clears the token, and issues a
   JWT (auto-login). Frontend redirects to the dashboard.
3. `POST /auth/login` on an unverified account throws `EmailNotVerifiedException`
   → controller maps to **403** `{ code: "email_not_verified" }`. Frontend shows
   an inline "resend verification" affordance.
4. `POST /auth/resend-verification { email }` — always returns **200** (no account
   enumeration). If an unverified account exists and the last send was >60s ago,
   regenerates the token + expiry and resends.

## Decisions (approved defaults)

| # | Decision |
|---|----------|
| A | Register returns `verification_sent` (no auto-login / no JWT). |
| B | Clicking the verify link auto-logs-in (verify endpoint issues a JWT). |
| C | Admins via config `Auth:AdminEmails`, default `["higor@waao.com.br"]` (case-insensitive). |
| D | Resend rate-limit: 60s between sends per account (`LastVerificationEmailSentAt`). |
| E | Resend endpoint + frontend button included (needed for a usable flow). |
| F | Dev fallback: when `Resend:ApiKey` is empty, log the verification link instead of sending. |

## Backend changes (`Waao.API` / `.Services` / `.Domain.Models` / `.Infra.EF`)

### Entity — `Collaborator` (+5 fields, Auth section)
- `bool EmailVerified` — default `false`
- `string? EmailVerificationToken`
- `DateTime? EmailVerificationTokenExpiresAt`
- `DateTime? EmailVerifiedAt`
- `DateTime? LastVerificationEmailSentAt`

### Migration — `AddEmailVerification`
- New columns with safe defaults (`email_verified` NOT NULL DEFAULT false; rest nullable).
- **Data backfill:** `UPDATE collaborators SET email_verified = true;` for all
  pre-existing rows (seeded admin/hr/demo and any earlier real users predate
  verification and must not be locked out).
- Index on `email_verification_token` (lookup by token on verify).
- Applied to local Postgres (`WaaoLocal`) before commit.

### Seeder — `DbInitializer`
- New seeded users get `EmailVerified = true`, `EmailVerifiedAt = UtcNow`.

### DTOs (`AuthDtos.cs`)
- `record VerifyEmailDto { string Token }`
- `record ResendVerificationDto { string Email }`
- `record RegisterResultDto { string Status; string Email }` (register return type)
- `RegisterDto` unchanged. `LoginAsync` still returns `AuthResultDto`.
- Error contract: `EmailNotVerifiedException` → 403 body `{ code: "email_not_verified", message }`.

### Validators (`AuthValidators.cs`)
- `RegisterValidator`: add rule — `Email` must match `^[^@\s]+@waao\.com\.br$`
  (case-insensitive), with a clear i18n-friendly message key.
- `VerifyEmailValidator`: `Token` not empty.
- `ResendVerificationValidator`: `Email` not empty + valid email shape.

### `AuthService`
- `RegisterAsync`: domain enforced by validator; reject duplicate email as today;
  role = `Admin` if email ∈ `Auth:AdminEmails` (case-insensitive) else
  `Collaborator`; create unverified; generate token (`RandomNumberGenerator`,
  32 bytes, base64url) + `ExpiresAt = UtcNow+24h`; set `LastVerificationEmailSentAt`;
  call `IEmailSender`; return `RegisterResultDto`. Day-one badges/streak logic that
  currently runs in register **moves to first successful verify/login** (no
  gamification side-effects for an unverified account).
- `LoginAsync`: after password check, `if (!EmailVerified) throw new EmailNotVerifiedException(email);`
- `VerifyEmailAsync(VerifyEmailDto)`: load by token; if none or
  `ExpiresAt < UtcNow` → `InvalidVerificationTokenException` (400
  `{ code: "invalid_or_expired_token" }`); else set verified, clear token,
  run the day-one badges/streak pass, issue JWT, return `AuthResultDto`.
- `ResendVerificationAsync(ResendVerificationDto)`: load by email; if exists and
  unverified and `LastVerificationEmailSentAt` is null or >60s ago → new token +
  expiry, update `LastVerificationEmailSentAt`, send; **always** complete with 200
  (no enumeration, no error if not found / already verified / rate-limited).

### Email sending
- `interface IEmailSender { Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct); }`
  in `Waao.Services.Abstractions`.
- `ResendEmailSender` in `Waao.Services` — `HttpClient` POST to
  `https://api.resend.com/emails`, `Authorization: Bearer {Resend:ApiKey}`,
  JSON `{ from, to, subject, html }`. HTML body: branded WAAO verification
  message + button/link. On non-success status → log + throw (register/resend
  surface a 502-style error to the client except resend which still returns 200).
- `LoggingEmailSender` fallback registered when `Resend:ApiKey` is empty — logs
  `verifyUrl` at Information level.
- DI: `builder.Services.AddHttpClient<ResendEmailSender>()`; pick sender by config.
- Config keys: `Resend:ApiKey` (Fly secret `Resend__ApiKey`), `Email:From`
  (e.g. `WAAO <no-reply@waao.com.br>`), `Frontend:BaseUrl`
  (`https://waao-frontend.pages.dev`), `Auth:AdminEmails`.

### Controller (`AuthController`)
- `POST /auth/verify-email` `[AllowAnonymous]` → `AuthResultDto` (200) / 400.
- `POST /auth/resend-verification` `[AllowAnonymous]` → 200 (always).
- `register` now returns `RegisterResultDto` (201). `login` 403 mapping handled
  by the existing exception middleware (extend `ExceptionHandlingMiddleware` to
  map the two new exception types to coded JSON).

## Frontend changes (`WaaoFrontend`, own UI lib)

- `auth.service.ts`: `verifyEmail(token)`, `resendVerification(email)`; `register`
  return type → `{ status: string; email: string }`.
- `register-page.tsx`: client-side `@waao.com.br` check (mirrors server message);
  on success swap form for a "Check your email — we sent a link to {email}"
  panel with a **Resend** button (disabled 60s after each send).
- New `verify-email-page.tsx`, public route `/verify-email`: read `?token`,
  call `verifyEmail`, on success store JWT + redirect to dashboard; on
  invalid/expired show message + email field + Resend.
- `login-page.tsx`: catch `email_not_verified` → inline notice + Resend control.
- Router: register `/verify-email` as a public (unauthenticated) route.
- i18n: new keys added to **all three** `src/locales/{pt-BR,en,es}/common.json`
  (auth.verify.*, auth.register.domainHint, auth.login.notVerified, etc.),
  pt-BR is the fallback.

## Error contract

| Case | HTTP | Body |
|------|------|------|
| Register, non-`@waao.com.br` | 400 | FluentValidation failure on `email` |
| Register, email already used | 400 | existing "Email is already in use." |
| Login, unverified | 403 | `{ code: "email_not_verified" }` |
| Verify, bad/expired token | 400 | `{ code: "invalid_or_expired_token" }` |
| Resend (any state) | 200 | `{ status: "ok" }` |

## Testing

- **Backend unit tests** (`IEmailSender` mocked, in-memory/SQLite or test Postgres):
  - register rejects non-domain email
  - register assigns `Admin` for a configured email, `Collaborator` otherwise
  - register creates unverified row with token + expiry + sends one email
  - login throws `EmailNotVerifiedException` while unverified, succeeds after verify
  - verify: happy path sets verified + issues JWT; expired token rejected;
    unknown token rejected; token single-use (cleared after success)
  - resend: returns ok for unknown/verified/rate-limited; regenerates token when allowed
- **Migration**: applied against local `WaaoLocal`; verify backfill set existing
  rows to `email_verified = true`.
- **Frontend**: manual smoke — register → "check email" → log link → verify page
  (valid, expired) → auto-login; login of unverified shows resend; resend cooldown.

## Operational notes

- Resend requires a verified sender domain. Add `waao.com.br` (or a subdomain) in
  the Resend dashboard and the DNS records before real emails send; until then the
  logging fallback (no API key) is used.
- New Fly secret: `fly secrets set --app waao-api Resend__ApiKey="re_…"`.
- `Frontend:BaseUrl` must match the live Cloudflare Pages URL; if the Pages
  project URL differs from `https://waao-frontend.pages.dev`, set it via config/secret.

## Rollout / safety

- Greenfield-ish but **production data exists** (seeded + any real signups): the
  backfill `UPDATE … SET email_verified = true` is mandatory and must run in the
  same migration so no existing user is locked out.
- Migration reviewed before `dotnet ef database update`; deploy backend, then
  frontend (Cloudflare auto-deploys on push).
