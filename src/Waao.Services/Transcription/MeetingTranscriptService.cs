using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Transcription;

public sealed class MeetingTranscriptService(WaaoDbContext Db) : IMeetingTranscriptService
{
	public async Task IngestAsync(Guid meetingId, IngestTranscriptDto dto, CancellationToken ct = default)
	{
		// Verify meeting exists
		var meetingExists = await Db.Meetings.AnyAsync(m => m.Id == meetingId, ct);
		if (!meetingExists)
			throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		// Overwrite: soft-delete any existing transcript (and its lines via cascade is DB-side,
		// but we soft-delete to preserve the soft-delete invariant)
		var existing = await Db.MeetingTranscripts
			.Include(t => t.Lines)
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(t => t.MeetingId == meetingId, ct);

		if (existing is not null)
		{
			foreach (var line in existing.Lines)
			{
				line.IsDeleted = true;
				line.DeletedAt = DateTime.UtcNow;
			}
			existing.IsDeleted = true;
			existing.DeletedAt = DateTime.UtcNow;
			await Db.SaveChangesAsync(ct);
		}

		// Resolve speakers: keep SpeakerId only when a live collaborator exists
		var speakerIds = dto.Lines
			.Where(l => l.SpeakerId.HasValue)
			.Select(l => l.SpeakerId!.Value)
			.Distinct()
			.ToList();

		var validSpeakerIds = speakerIds.Count > 0
			? (await Db.Collaborators
				.Where(c => speakerIds.Contains(c.Id))
				.Select(c => c.Id)
				.ToListAsync(ct))
				.ToHashSet()
			: [];

		var transcript = new MeetingTranscript
		{
			Id = Guid.CreateVersion7(),
			MeetingId = meetingId,
			GeneratedAtUtc = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
		};
		Db.MeetingTranscripts.Add(transcript);

		var lines = dto.Lines.Select(l => new MeetingTranscriptLine
		{
			Id = Guid.CreateVersion7(),
			TranscriptId = transcript.Id,
			SpeakerCollaboratorId = l.SpeakerId.HasValue && validSpeakerIds.Contains(l.SpeakerId.Value)
				? l.SpeakerId
				: null,
			SpeakerName = l.SpeakerName,
			Text = l.Text,
			OffsetSeconds = l.OffsetSeconds,
			CreatedAt = DateTime.UtcNow,
		}).ToList();

		Db.MeetingTranscriptLines.AddRange(lines);
		await Db.SaveChangesAsync(ct);
	}

	public async Task<MeetingTranscriptDto?> GetAsync(Guid meetingId, Guid callerId, CancellationToken ct = default)
	{
		// Verify the meeting exists and check access
		var meeting = await Db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct)
			?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

		var isOrganizerOrAttendee = meeting.OrganizerId == callerId
			|| await Db.MeetingAttendees.AnyAsync(a => a.MeetingId == meetingId && a.CollaboratorId == callerId, ct);

		if (!isOrganizerOrAttendee)
			throw new UnauthorizedAccessException($"Caller {callerId} does not have access to transcript for meeting {meetingId}.");

		var transcript = await Db.MeetingTranscripts
			.Include(t => t.Lines)
			.FirstOrDefaultAsync(t => t.MeetingId == meetingId, ct);

		if (transcript is null)
			return null;

		return new MeetingTranscriptDto
		{
			MeetingId = transcript.MeetingId,
			GeneratedAtUtc = transcript.GeneratedAtUtc,
			Lines = transcript.Lines
				.Where(l => !l.IsDeleted)
				.OrderBy(l => l.OffsetSeconds)
				.Select(l => new MeetingTranscriptLineDto
				{
					SpeakerCollaboratorId = l.SpeakerCollaboratorId,
					SpeakerName = l.SpeakerName,
					Text = l.Text,
					OffsetSeconds = l.OffsetSeconds,
				})
				.ToList(),
		};
	}
}
