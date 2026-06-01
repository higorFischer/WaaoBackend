using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.FeatureRequests;
using Waao.Domain.Models.Entities.Kanban;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Entities.Notifications;
using Waao.Domain.Models.Entities.Focus;
using Waao.Domain.Models.Entities.Kudos;
using Waao.Domain.Models.Entities.OneOnOnes;
using Waao.Domain.Models.Entities.TimeOff;

namespace Waao.Infra.EF;

public class WaaoDbContext(DbContextOptions<WaaoDbContext> Options) : DbContext(Options)
{
	public DbSet<Collaborator> Collaborators => Set<Collaborator>();
	public DbSet<Department> Departments => Set<Department>();
	public DbSet<Role> Roles => Set<Role>();
	public DbSet<CareerEvent> CareerEvents => Set<CareerEvent>();
	public DbSet<Badge> Badges => Set<Badge>();
	public DbSet<CollaboratorBadge> CollaboratorBadges => Set<CollaboratorBadge>();
	public DbSet<XpTransaction> XpTransactions => Set<XpTransaction>();
	public DbSet<LevelDefinition> LevelDefinitions => Set<LevelDefinition>();

	// Courses
	public DbSet<Course> Courses => Set<Course>();
	public DbSet<CourseCompletion> CourseCompletions => Set<CourseCompletion>();

	// Challenges
	public DbSet<Challenge> Challenges => Set<Challenge>();
	public DbSet<ChallengeQuestion> ChallengeQuestions => Set<ChallengeQuestion>();
	public DbSet<ChallengeAttempt> ChallengeAttempts => Set<ChallengeAttempt>();
	public DbSet<ChallengeAttemptAnswer> ChallengeAttemptAnswers => Set<ChallengeAttemptAnswer>();

	// Calendar
	public DbSet<Calendar> Calendars => Set<Calendar>();
	public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
	public DbSet<EventOccurrenceOverride> EventOccurrenceOverrides => Set<EventOccurrenceOverride>();

	// Meetings
	public DbSet<Meeting> Meetings => Set<Meeting>();
	public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
	public DbSet<MeetingAgendaItem> MeetingAgendaItems => Set<MeetingAgendaItem>();
	public DbSet<MeetingTranscript> MeetingTranscripts => Set<MeetingTranscript>();
	public DbSet<MeetingTranscriptLine> MeetingTranscriptLines => Set<MeetingTranscriptLine>();

	// Messaging
	public DbSet<Channel> Channels => Set<Channel>();
	public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();
	public DbSet<Message> Messages => Set<Message>();
	public DbSet<MessageMention> MessageMentions => Set<MessageMention>();

	// Notifications
	public DbSet<Notification> Notifications => Set<Notification>();
	public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

	// Kanban
	public DbSet<Board> Boards => Set<Board>();
	public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
	public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();
	public DbSet<Epic> Epics => Set<Epic>();
	public DbSet<Card> Cards => Set<Card>();
	public DbSet<CardLabel> CardLabels => Set<CardLabel>();
	public DbSet<CardLabelMap> CardLabelMaps => Set<CardLabelMap>();
	public DbSet<CardComment> CardComments => Set<CardComment>();
	public DbSet<CardChecklist> CardChecklists => Set<CardChecklist>();
	public DbSet<CardChecklistItem> CardChecklistItems => Set<CardChecklistItem>();
	public DbSet<CardActivity> CardActivities => Set<CardActivity>();

	// ----- Allocation board -----
	public DbSet<Project> Projects => Set<Project>();
	public DbSet<ProjectAllocation> ProjectAllocations => Set<ProjectAllocation>();
	public DbSet<Waao.Domain.Models.Entities.Allocation.ProjectConnection> ProjectConnections => Set<Waao.Domain.Models.Entities.Allocation.ProjectConnection>();
	public DbSet<Waao.Domain.Models.Entities.Allocation.ProjectAllocationEvent> ProjectAllocationEvents => Set<Waao.Domain.Models.Entities.Allocation.ProjectAllocationEvent>();

	// Feature requests
	public DbSet<FeatureRequest> FeatureRequests => Set<FeatureRequest>();
	public DbSet<FeatureRequestVote> FeatureRequestVotes => Set<FeatureRequestVote>();
	public DbSet<FeatureRequestComment> FeatureRequestComments => Set<FeatureRequestComment>();

	// Time Off
	public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();

	// Kudos
	public DbSet<Kudo> Kudos => Set<Kudo>();
	public DbSet<KudoRecipient> KudoRecipients => Set<KudoRecipient>();

	// Weekly Focus (admin-curated)
	public DbSet<WeeklyFocus> WeeklyFocuses => Set<WeeklyFocus>();
	public DbSet<WeeklyFocusGoal> WeeklyFocusGoals => Set<WeeklyFocusGoal>();
	public DbSet<WeeklyFocusProject> WeeklyFocusProjects => Set<WeeklyFocusProject>();

	// 1:1s (manager <-> report meetings)
	public DbSet<OneOnOne> OneOnOnes => Set<OneOnOne>();
	public DbSet<OneOnOneActionItem> OneOnOneActionItems => Set<OneOnOneActionItem>();

	// Internal feedback ("what's happening inside the company")
	public DbSet<Waao.Domain.Models.Entities.Feedback.Feedback> Feedback => Set<Waao.Domain.Models.Entities.Feedback.Feedback>();

	// Messaging — attachments
	public DbSet<Waao.Domain.Models.Entities.Messaging.MessageAttachment> MessageAttachments => Set<Waao.Domain.Models.Entities.Messaging.MessageAttachment>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaaoDbContext).Assembly);
	}
}
