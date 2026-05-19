using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos;

public record CareerEventDto
{
	public Guid Id { get; init; }
	public Guid CollaboratorId { get; init; }
	public CareerEventType Type { get; init; }
	public DateOnly EventDate { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Notes { get; init; }
	public string? FromValue { get; init; }
	public string? ToValue { get; init; }
	public decimal? XpAwarded { get; init; }
	public string? AttachmentUrl { get; init; }
}

public record CreateCareerEventDto
{
	public Guid CollaboratorId { get; init; }
	public CareerEventType Type { get; init; }
	public DateOnly EventDate { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Notes { get; init; }
	public string? FromValue { get; init; }
	public string? ToValue { get; init; }
	public string? AttachmentUrl { get; init; }
}
