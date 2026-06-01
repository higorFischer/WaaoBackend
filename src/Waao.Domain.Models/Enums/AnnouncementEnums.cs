namespace Waao.Domain.Models.Enums;

public enum AnnouncementAudience
{
	Everyone = 0,
	Department = 1,
	Role = 2,
	Specific = 3,
}

public enum RecurrenceKind
{
	None = 0,
	Daily = 1,
	Weekly = 2,
	Monthly = 3,
}

public enum AnnouncementEffect
{
	None = 0,
	Pulse = 1,
	Glow = 2,
	Confetti = 3,
	Marquee = 4,
}
