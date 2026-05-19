namespace Waao.Services.Abstractions.Dtos;

public record OnboardingStatusDto
{
	public bool Completed { get; init; }
	public DateTime? CompletedAt { get; init; }
	public bool PhotoSet { get; init; }
	public bool BioSet { get; init; }
	public bool BirthdateSet { get; init; }
	public bool DepartmentSet { get; init; }
}

public record CompleteOnboardingDto
{
	public string PhotoUrl { get; init; } = string.Empty;
	public string Bio { get; init; } = string.Empty;
	public DateOnly Birthdate { get; init; }
	public Guid DepartmentId { get; init; }
}
