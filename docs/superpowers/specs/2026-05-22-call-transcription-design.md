# WAAO — Call Transcription (pt-BR, speaker-attributed)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** new `WaaoLiveKitAgent` (Python worker), `WaaoBackend`, `WaaoFrontend`
- **Module:** Meetings / Video

## Goal

Transcribe WAAO video calls in **Brazilian Portuguese**, attributed by speaker, and save the transcript to the meeting so anyone with access can review "what happened and who said what" after the call.

## Locked decisions

| # | Decision |
|---|----------|
| Output | A saved per-meeting transcript (speaker + timestamp + text). No live captions in v1. |
| STT | Deepgram, `nova-2` model, `language=pt-BR`. |
| Collection | Server-side — a LiveKit **Agent** worker collects the transcript (survives clients dropping). Not browser-collected. |
| Persistence | One transcript per meeting; a re-called meeting overwrites it. |
| Speaker identity | The agent subscribes to each participant's audio track separately; participant identity = the WAAO collaborator id (the video JWT `sub`), resolved to a name backend-side. |

## Part 1 — `WaaoLiveKitAgent` (new repo, Python)

A `livekit-agents` worker deployed as its own Fly app (`waao-livekit-agent`).

- `agent.py` — entrypoint registered with the `waao-livekit` SFU; auto-dispatched to every room.
- On a room job: for each remote participant's microphone audio track, open a Deepgram STT stream (`livekit-plugins-deepgram`, `model="nova-2"`, `language="pt-BR"`, interim results off / finals only).
- Each finalized result appends a line: `{ speakerId: participant.identity, speakerName: participant.name, text, offsetSeconds }` (offset from room/agent start).
- Room name format is `waao-<meetingId-32hex>` — the agent parses the meeting id from it.
- On the job ending (room closed / all participants gone), POST the accumulated transcript to WAAO:
  `POST <WAAO_API>/api/waao/meetings/<meetingId>/transcript`
  header `X-Transcription-Key: <ingest key>`, body `{ "lines": [ {speakerId, speakerName, text, offsetSeconds}, ... ] }`.
  If there are zero lines, skip the POST.
- `Dockerfile` (python:3.12-slim + deps), `fly.toml`, `README.md`.
- Env: `LIVEKIT_URL` (the SFU wss), `LIVEKIT_API_KEY`, `LIVEKIT_API_SECRET`, `DEEPGRAM_API_KEY`, `WAAO_API_URL`, `TRANSCRIPTION_INGEST_KEY`.

## Part 2 — `WaaoBackend`

### Entities (`Waao.Domain.Models/Entities/Meetings/`)

`MeetingTranscript`:
- `Id` (Guid v7), `MeetingId` (FK → Meeting, **unique** — one per meeting), `GeneratedAtUtc`
- audit + soft-delete
- `Lines` collection.

`MeetingTranscriptLine`:
- `Id` (Guid v7), `TranscriptId` (FK)
- `SpeakerCollaboratorId` (Guid?, → Collaborator — null if the speaker isn't a known collaborator)
- `SpeakerName` (≤160 — denormalized; what the agent reported)
- `Text` (≤4000)
- `OffsetSeconds` (int — ordering / playback offset)
- audit + soft-delete

### Migration

`AddMeetingTranscript` — additive. Indexes: `MeetingTranscripts.MeetingId` unique; `MeetingTranscriptLines.TranscriptId`.

### Config

`TranscriptionOptions { string IngestKey }` bound from section `Transcription`. Fly secret `Transcription__IngestKey` on `waao-api`. The same value is set on the agent app as `TRANSCRIPTION_INGEST_KEY`.

### DTOs (`Dtos/Meetings/`)

- `IngestTranscriptDto { IReadOnlyList<IngestTranscriptLineDto> Lines }`
- `IngestTranscriptLineDto { Guid? SpeakerId, string SpeakerName, string Text, int OffsetSeconds }`
- `MeetingTranscriptDto { Guid MeetingId, DateTime GeneratedAtUtc, IReadOnlyList<MeetingTranscriptLineDto> Lines }`
- `MeetingTranscriptLineDto { Guid? SpeakerCollaboratorId, string SpeakerName, string Text, int OffsetSeconds }`

### Service — `IMeetingTranscriptService`

- `IngestAsync(Guid meetingId, IngestTranscriptDto dto, CancellationToken ct)` — verify the meeting exists; delete any existing `MeetingTranscript` for it (overwrite); create a new one + lines; `GeneratedAtUtc = now`. For each line, if `SpeakerId` resolves to a live collaborator keep it, else leave null.
- `GetAsync(Guid meetingId, Guid callerId, CancellationToken ct)` → `MeetingTranscriptDto?` — caller must organize/attend the meeting (else 403/404); null if no transcript yet.

### Controller

On `MeetingsController`:
- `POST "{id:guid}/transcript"` — `[AllowAnonymous]` at the action level (the agent is not a JWT user); the action **validates the `X-Transcription-Key` header** against `TranscriptionOptions.IngestKey` and 401s on mismatch. Body `IngestTranscriptDto`.
- `GET "{id:guid}/transcript"` — `[Authorize]` (inherited); → `MeetingTranscriptDto` or 204/empty when none.

## Part 3 — `WaaoFrontend`

- `src/types/meeting.types.ts` — `MeetingTranscript`, `MeetingTranscriptLine`.
- `src/services/meeting.service.ts` — `getTranscript(meetingId): Promise<MeetingTranscript | null>`.
- `src/pages/calendar/meeting-detail.tsx` — a **Transcript** section: when a transcript exists, render lines (speaker name, `mm:ss` offset, text) in order, grouped by consecutive same-speaker runs; when none, an empty state ("Transcrição aparece após a chamada de vídeo da reunião").
- i18n `meetings.transcript.*` × 3 locales (pt-BR canonical).

## Error contract

| Case | HTTP |
|------|------|
| Ingest with a missing/wrong `X-Transcription-Key` | 401 |
| Ingest for an unknown meeting | 404 |
| Get transcript for a meeting the caller doesn't organize/attend | 403 |
| Get when no transcript exists | 200 with empty body / null |

## Testing

- Backend: `IngestAsync` creates a transcript + lines; a second ingest **overwrites**; unknown `SpeakerId` → line kept with null `SpeakerCollaboratorId`. `GetAsync` access-checks. The ingest-key header guard 401s on mismatch.
- Agent: a smoke run against the SFU is manual (needs a real call); unit-test the room-name → meetingId parsing.

## Rollout / cost

- New tables only (additive migration) on `waao-api`.
- New Fly app `waao-livekit-agent` — small always-on worker.
- **Operator steps:** create a Deepgram account → `DEEPGRAM_API_KEY`; choose an `IngestKey` secret; set `Transcription__IngestKey` on `waao-api` and `TRANSCRIPTION_INGEST_KEY` + the LiveKit creds + `DEEPGRAM_API_KEY` on `waao-livekit-agent`.
- **Cost:** Deepgram streaming STT ≈ US$0.0043/audio-minute (free credit covers initial use). Per-participant audio = N× minutes for an N-person call.

## Out of scope (v1)

- Live captions during the call
- Transcript editing, AI summary / action items, search across transcripts
- Multi-language / language auto-detect (fixed pt-BR)
- Per-call-session history (latest call overwrites)
- Recording the audio/video itself
