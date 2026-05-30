using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Kudos;

public record KudoRecipientDto
{
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public string? CollaboratorPhotoUrl { get; init; }
}

public record KudoDto
{
	public Guid Id { get; init; }
	public Guid GiverId { get; init; }
	public string GiverName { get; init; } = string.Empty;
	public string? GiverPhotoUrl { get; init; }
	public KudoValue Value { get; init; }
	public string Message { get; init; } = string.Empty;
	public DateTime CreatedAtUtc { get; init; }
	public IReadOnlyList<KudoRecipientDto> Recipients { get; init; } = [];
}

public record GiveKudoDto
{
	public IReadOnlyList<Guid> RecipientIds { get; init; } = [];
	public KudoValue Value { get; init; }
	public string Message { get; init; } = string.Empty;
}

public record KudoFeedDto
{
	public IReadOnlyList<KudoDto> Kudos { get; init; } = [];
	public bool HasMore { get; init; }
}
