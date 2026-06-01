namespace Waao.Domain.Models.Enums;

public enum NotificationKind
{
	Mention = 0,
	ChannelInvite = 1,
	MeetingInvite = 2,
	MeetingUpdated = 3,
	MeetingCancelled = 4,
	FeatureRequestStatus = 5,
	TimeOffRequested = 6,
	TimeOffApproved = 7,
	TimeOffRejected = 8,
	KudoReceived = 9,
	BadgeAwarded = 10,
	SystemAnnouncement = 11,
	OneOnOneScheduled = 12,
	OneOnOneActionItemAssigned = 13,
}
