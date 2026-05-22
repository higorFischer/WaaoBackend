# Call Transcription Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox steps.

**Goal:** Save a pt-BR, speaker-attributed transcript per WAAO video call — a LiveKit Agent worker collects it via Deepgram and POSTs it to `waao-api`; the meeting detail page shows it.

**Spec:** `docs/superpowers/specs/2026-05-22-call-transcription-design.md` — read first.

**Conventions:** Backend TABS, file-scoped namespaces, primary-ctor DI, `record` DTOs, `Guid.CreateVersion7()`, `DateTime.UtcNow`, soft delete; migrations via Bash heredoc. Frontend no `any`, `t()` × 3 locales. Python: PEP8, typed. Commit `git -c user.email="higor@waao.com.br"`, conventional prefix, no Claude/AI/Co-Authored-By.

---

## Task 1: Backend — entities + migration

- `MeetingTranscript`, `MeetingTranscriptLine` entities; EF configs (unique `MeetingId`, index on `TranscriptId`); DbSets in `WaaoDbContext`.
- Migration `AddMeetingTranscript` (heredoc-authored; apply to `WaaoLocal`).
- [ ] Implement; `dotnet build src/Waao.API/Waao.API.csproj` clean
- [ ] Commit: `feat(transcription): entities + migration`

## Task 2: Backend — DTOs, options, service

- DTOs per spec in `Dtos/Meetings/`.
- `TranscriptionOptions` bound from section `Transcription`; register in `Program.cs`.
- `IMeetingTranscriptService` + impl — `IngestAsync` (overwrite semantics, resolve speakers) + `GetAsync` (access check).
- [ ] TDD: ingest creates transcript+lines; second ingest overwrites; unknown speaker → null id; GetAsync access-checked.
- [ ] `dotnet build` + `dotnet test` green
- [ ] Commit: `feat(transcription): transcript service`

## Task 3: Backend — endpoints

- `MeetingsController`: `POST "{id:guid}/transcript"` (`[AllowAnonymous]`, validates `X-Transcription-Key` header → 401 on mismatch) + `GET "{id:guid}/transcript"` (`[Authorize]`).
- [ ] `dotnet build` clean, `dotnet test` green
- [ ] `git push origin main`
- [ ] Commit: `feat(transcription): ingest + get endpoints`

## Task 4: WaaoLiveKitAgent — new Python repo

Create `/Users/higorflopes/RiderProjects/Repositories/Waao/WaaoLiveKitAgent`:
- `requirements.txt` — `livekit-agents`, `livekit-plugins-deepgram`, `httpx`.
- `agent.py` — `livekit-agents` worker; auto-dispatch to rooms; per-participant Deepgram STT (`model=nova-2`, `language=pt-BR`); accumulate lines; on job end POST to `<WAAO_API>/api/waao/meetings/<meetingId>/transcript` with `X-Transcription-Key`. Parse meetingId from the `waao-<32hex>` room name.
- `room_name.py` (or inline) — pure `meeting_id_from_room(room_name)` helper.
- `Dockerfile`, `fly.toml` (app `waao-livekit-agent`), `.gitignore`, `README.md` (env vars, deploy, the Deepgram + ingest-key setup).
- [ ] Implement; a unit test for the room-name parser (`python -m pytest` or a simple assert script)
- [ ] `git init`, commit: `feat: WAAO LiveKit transcription agent`
- [ ] (Operator) create the GitHub repo + push; deploy to Fly with the env/secrets.

## Task 5: Frontend — transcript on the meeting detail

- `meeting.types.ts` — `MeetingTranscript`, `MeetingTranscriptLine`.
- `meeting.service.ts` — `getTranscript(meetingId)`.
- `meeting-detail.tsx` — a Transcript section: lines (speaker, `mm:ss`, text) grouped by consecutive same-speaker runs; empty state otherwise.
- `meetings.transcript.*` i18n × 3 locales (pt-BR canonical).
- [ ] `npm run build` clean
- [ ] `git push origin main`
- [ ] Commit: `feat(transcription): transcript view on the meeting detail`

---

## Self-review
- Spec coverage: entities/migration→T1, service→T2, endpoints→T3, agent→T4, frontend→T5. ✓
- `IngestTranscriptDto`/`MeetingTranscriptDto` shapes consistent backend (T2) ↔ agent POST body (T4) ↔ frontend types (T5). ✓
- No placeholders. ✓
