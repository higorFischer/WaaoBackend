# WAAO — Challenges (multiple-choice quizzes)

- **Date:** 2026-05-19
- **Status:** Approved (design)
- **Sequence:** Feature **F**. Ships in the same sweep as Feature E (Courses) per user direction. Both share the admin-grants-XP pattern and feed the SkillRadar through `XpTransaction.reason`.

## Goal

WAAO-native multi-choice quiz catalog. Admin (or HR) authors challenges with 4-option questions and a correct answer per question. Collaborators take a challenge; the backend grades on submit. Each completed attempt lands in an admin review queue; admin grants XP (suggested amount defaults to the challenge's `SuggestedXp` scaled by the attempt's score). Same `XpSource.Admin`, same category-tagged `reason` → SkillRadar lights up.

## Non-goals
- Open-ended / free-text answers
- Timers per question (only a soft per-attempt time-spent record)
- Randomized question order or option shuffling (deterministic v1)
- Multiple correct answers per question (single correct answer only)
- Auto-grant of XP on pass — XP stays admin-only

## Locked decisions

| # | Decision |
|---|----------|
| Question shape | 4 options per question, exactly 1 correct |
| Pass criterion | `PassPercent` per challenge (default 70). Attempts below the threshold are still reviewable; admin decides whether to grant any XP. |
| Attempts | Unlimited. Each attempt creates a row in `challenge_attempts`. Admin sees the latest *passing* attempt per (challenge, collaborator) in the review queue, plus a "history" link to all attempts. |
| XP | Admin grants from the queue. Default amount = `Math.Round(challenge.SuggestedXp * (attempt.ScorePct / 100))`. Reason format: `"Challenge passed: <Title> [Category: <Category>] (<ScorePct>%)"`. |
| Authoring | Admin/HR (reuse `Policy="HR"`). Same draft/publish gate as courses (`IsPublished=false` by default). |
| Category | Required, same free-text taxonomy as Courses (feeds the same radar matcher). |

## Backend changes

### Entities

`Challenge`:
- `Id` (Guid v7), `Title` ≤200, `Description` ≤2000, `Category` ≤80 (required),
  `SuggestedXp` int? 0..10000, `PassPercent` int default 70 (50..100),
  `IsPublished` bool default false, `CreatedById` Guid, audit + soft-delete.

`ChallengeQuestion`:
- `Id`, `ChallengeId` FK, `Order` int, `Prompt` ≤500,
  `OptionA` / `OptionB` / `OptionC` / `OptionD` ≤200 each,
  `CorrectOption` char(1) ∈ {A,B,C,D}, audit + soft-delete.
- Unique `(ChallengeId, Order)` where not deleted.

`ChallengeAttempt`:
- `Id`, `ChallengeId` FK, `CollaboratorId` FK,
  `StartedAt`, `SubmittedAt`, `ScorePct` int (0..100), `Passed` bool,
  `XpAwarded` int?, `XpAwardedAt` DateTime?, `XpAwardedByAdminId` Guid?,
  audit + soft-delete.
- Index `(CollaboratorId, ChallengeId)`.
- Partial: `(SubmittedAt, Passed)` `WHERE Passed = true AND XpAwardedAt IS NULL AND IsDeleted = false` — pending admin review.

`ChallengeAttemptAnswer`:
- `Id`, `AttemptId` FK, `QuestionId` FK, `SelectedOption` char(1), `IsCorrect` bool, audit + soft-delete.

### Migration

`AddChallenges` — additive. Indexes:
- `Challenges.(IsPublished, IsDeleted)`, `Challenges.Category`
- `ChallengeQuestions.ChallengeId`
- `ChallengeAttempts.(CollaboratorId, IsDeleted)`, partial pending-review index
- `ChallengeAttemptAnswers.AttemptId`

### Services

`IChallengeService`:
- CRUD + `PublishAsync` (Admin/HR — same as Courses).
- `ListAsync(isAdminOrHr, ct)`, `GetByIdAsync(id, isAdminOrHr, ct)`. Non-admin only sees published.
- For collaborator view, the service strips `CorrectOption` from `ChallengeQuestionDto` (returns a `PublicChallengeQuestionDto` without the answer).

`IChallengeQuestionService` (folded into `ChallengeService` for v1 to keep surface minimal):
- `AddQuestionAsync`, `UpdateQuestionAsync`, `DeleteQuestionAsync`, `ReorderQuestionsAsync(challengeId, Guid[] orderedIds)`.

`IChallengeAttemptService`:
- `StartAsync(challengeId, collaboratorId, ct)` — creates an in-progress attempt (SubmittedAt=null). Returns the challenge with public questions only.
- `SubmitAsync(attemptId, SubmitChallengeAttemptDto answers, collaboratorId, ct)` — grades server-side, fills `ScorePct`, `Passed`, persists per-question `ChallengeAttemptAnswer` rows with `IsCorrect`, returns `ChallengeAttemptResultDto { ScorePct, Passed, CorrectCount, TotalCount, PerQuestion: [{ questionId, selected, isCorrect, correctOption }] }`.
  - The result reveals correct answers so the collaborator gets feedback. (Honesty over leakage — this is an internal training tool, not a credentialing exam.)
- `ListPendingForReviewAsync(ct)` (Admin) — pending pass-XP grants.
- `GrantXpForAttemptAsync(attemptId, GrantChallengeXpDto, adminId, ct)` (Admin) — same idempotency pattern as Courses.
- `ListMyAttemptsAsync(collaboratorId, ct)`.

### Controllers

`ChallengesController` `[Route("api/waao/challenges")] [Authorize]`:
- `GET ""` / `GET "{id:guid}"` — list + detail (role-flag pattern).
- `POST ""` / `PUT "{id:guid}"` / `DELETE "{id:guid}"` / `POST "{id:guid}/publish"` — CRUD (HR/Admin; delete is Admin).
- `POST "{id:guid}/questions"` / `PUT "{id:guid}/questions/{questionId:guid}"` / `DELETE "{id:guid}/questions/{questionId:guid}"` / `POST "{id:guid}/questions/reorder"` — question management (HR/Admin).
- `POST "{id:guid}/attempts"` — start an attempt (any authed collaborator).
- `POST "attempts/{attemptId:guid}/submit"` — submit answers.
- `GET "me/attempts"` — own attempts.

Admin queue (folded into existing `AdminController`):
- `GET "challenge-attempts/pending"` (Admin).
- `POST "challenge-attempts/{id:guid}/grant-xp"` (Admin).

### DTOs (key shapes)

- `ChallengeDto` — full incl. questions WITH `CorrectOption` (admin/HR only).
- `PublicChallengeDto` — questions WITHOUT `CorrectOption` (collaborator-facing).
- `CreateChallengeDto` — title/description/category/suggestedXp/passPercent.
- `CreateChallengeQuestionDto` — prompt + 4 options + correctOption.
- `SubmitChallengeAttemptDto { Answers: [{ QuestionId, SelectedOption }] }`.
- `ChallengeAttemptResultDto` — as above.
- `ChallengeAttemptDto` for admin review — incl. collaborator + challenge + score + suggested grant.
- `GrantChallengeXpDto { Amount }`.

### Validators

`CreateChallengeValidator`, `UpdateChallengeValidator`, `CreateChallengeQuestionValidator` (correctOption ∈ {A,B,C,D}, options non-empty), `SubmitChallengeAttemptValidator` (Answers non-empty, each SelectedOption ∈ {A,B,C,D}), `GrantChallengeXpValidator` (Amount > 0, ≤10000).

### DI / Program.cs
- Register `IChallengeService`, `IChallengeAttemptService`.

## Frontend

- Sidebar Challenges → `/challenges` (replace its coming-soon target).
- Pages:
  - `ChallengesPage` — list with category filter; cards show title, category, question count, best score (if attempted).
  - `ChallengeDetailPage` — description + "Start" CTA.
  - `ChallengeAttemptPage` — question-by-question (or single-page) form; on submit, calls `submit`, then shows the result screen (score, pass/fail, per-question feedback with correct answers).
  - `MyChallengesPage` — attempt history.
  - `AdminChallengesPage` (HR/Admin) — CRUD with nested question editor.
  - `AdminChallengeReviewPage` (Admin) — pending pass-XP grants, prefilled amount = `round(suggestedXp * scorePct/100)`.
- Services: `challenges.service.ts`, admin pieces fold into `admin.service.ts`.
- Types in `src/types/waao.types.ts`.
- i18n `challenges.*` namespace × 3 locales.

## Error contract

| Case | HTTP |
|------|------|
| Unknown challenge / unpublished to collaborator | 404 |
| Unknown attempt | 404 |
| Submit already-submitted attempt | 200 idempotent (returns existing result) |
| Submit with answers for foreign attempt (different user) | 403 |
| Grant-XP unknown attempt | 404 |
| Grant-XP already-awarded | 200 idempotent |
| Validation failures | 400 FluentValidation errors |

## Testing (Waao.Tests)

- CRUD + publish (Admin/HR enforcement).
- Question reorder.
- Attempt grading: scoring math, idempotent submit, foreign-user 403.
- `GrantXpForAttempt`: writes `XpTransaction` with `XpSource.Admin`, reason includes "Challenge passed: …", `sourceEntityType="ChallengeAttempt"`, sets award fields.
- Public challenge endpoint never returns `CorrectOption`.

## Rollout

- New tables only. Greenfield additive migration. Applied under existing startup advisory lock.
- Deploys via the new CI/CD (push to `main` → auto-deploy).
