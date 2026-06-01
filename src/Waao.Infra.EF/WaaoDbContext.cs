using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Services.Abstractions.Services;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.FeatureRequests;
using Waao.Domain.Models.Entities.Kanban;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Entities.Notifications;
using Waao.Domain.Models.Entities.Announcements;
using Waao.Domain.Models.Entities.Calls;
using Waao.Domain.Models.Entities.Focus;
using Waao.Domain.Models.Entities.Kudos;
using Waao.Domain.Models.Entities.OneOnOnes;
using Waao.Domain.Models.Entities.TimeOff;

namespace Waao.Infra.EF;

public class WaaoDbContext : DbContext
{
	// Captured at construction; EF re-reads this field per query via the expression
	// tree we build in OnModelCreating, so the filter always uses the LIVE tenant
	// for the current request scope.
	private readonly ITenantContext? _tenantContext;

	public WaaoDbContext(DbContextOptions<WaaoDbContext> options) : base(options) { }
	public WaaoDbContext(DbContextOptions<WaaoDbContext> options, ITenantContext tenantContext) : base(options)
	{
		_tenantContext = tenantContext;
	}

	/// <summary>
	/// Exposed so the query-filter expression can reference it. Returns null when
	/// no tenant context was injected (design-time tooling, migrations) — the
	/// filter then short-circuits to 'no filter' so EF tooling keeps working.
	/// </summary>
	public Guid? CurrentTenantId => _tenantContext?.CurrentTenantId;

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

	// Voice/video call channels (Discord-style persistent rooms)
	public DbSet<CallChannel> CallChannels => Set<CallChannel>();

	// Announcements (scheduled banners shown above the weekly focus)
	public DbSet<Announcement> Announcements => Set<Announcement>();
	public DbSet<AnnouncementTarget> AnnouncementTargets => Set<AnnouncementTarget>();

	// Internal feedback ("what's happening inside the company")
	public DbSet<Waao.Domain.Models.Entities.Feedback.Feedback> Feedback => Set<Waao.Domain.Models.Entities.Feedback.Feedback>();

	// Peer-to-peer feedback (collaborator -> collaborator, optionally anonymous)
	public DbSet<Waao.Domain.Models.Entities.Feedback.PeerFeedback> PeerFeedbacks => Set<Waao.Domain.Models.Entities.Feedback.PeerFeedback>();

	// Messaging — attachments
	public DbSet<Waao.Domain.Models.Entities.Messaging.MessageAttachment> MessageAttachments => Set<Waao.Domain.Models.Entities.Messaging.MessageAttachment>();

	// Messaging — emoji reactions (WhatsApp-style)
	public DbSet<Waao.Domain.Models.Entities.Messaging.MessageReaction> MessageReactions => Set<Waao.Domain.Models.Entities.Messaging.MessageReaction>();

	// Multi-tenancy boundary.
	public DbSet<Tenant> Tenants => Set<Tenant>();
	public DbSet<TenantAllowedEmailDomain> TenantAllowedEmailDomains => Set<TenantAllowedEmailDomain>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaaoDbContext).Assembly);

		// --- Multi-tenancy: shadow TenantId on every tenant-scoped entity + global query filter.
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (entityType.ClrType == typeof(Tenant)) continue;
			// Allowlist read pre-auth (registration) and pre-tenant-context — must
			// stay cross-tenant queryable. The entity owns TenantId as a real FK.
			if (entityType.ClrType == typeof(TenantAllowedEmailDomain)) continue;
			if (entityType.IsOwned()) continue;

			var tenantIdProp = entityType.FindProperty("TenantId");
			if (tenantIdProp is null)
			{
				modelBuilder.Entity(entityType.ClrType).Property<Guid?>("TenantId");
			}

			// Index so per-tenant filtering stays fast under the query filter.
			modelBuilder.Entity(entityType.ClrType).HasIndex("TenantId");

			// Phase 3: combine any existing query filter (e.g. !IsDeleted) with a tenant
			// check. Reflection-into-generic-helper because the loop variable isn't a type param.
			ApplyTenantFilterMethod
				.MakeGenericMethod(entityType.ClrType)
				.Invoke(this, [modelBuilder]);
		}
	}

	private static readonly MethodInfo ApplyTenantFilterMethod = typeof(WaaoDbContext)
		.GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

	/// <summary>
	/// Combines whatever query filter the entity's configuration already declared (typically
	/// soft-delete: <c>x => !x.IsDeleted</c>) with the tenant predicate.
	///
	/// Written as a real C# lambda so EF Core rewrites <c>CurrentTenantId</c> into a
	/// per-query DbContext parameter. Manual <c>Expression.Constant(this)</c> here would
	/// freeze the first instance — OnModelCreating runs once per DbContext type and the
	/// resulting model is cached, so a captured-instance constant breaks every later request.
	///
	/// The <c>== null</c> short-circuit is intentional: when no tenant is resolved (background
	/// services that haven't called <see cref="ITenantContext.SetTenant"/>, design-time
	/// tooling, EF migrations bootstrap), reads are NOT filtered. Production HTTP traffic
	/// always lands here with a tenant set by <c>TenantResolutionMiddleware</c>.
	/// </summary>
	private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
	{
		var entityBuilder = modelBuilder.Entity<TEntity>();
		var existing = entityBuilder.Metadata.GetQueryFilter();

		Expression<Func<TEntity, bool>> tenantPredicate = e =>
			CurrentTenantId == null ||
			Microsoft.EntityFrameworkCore.EF.Property<Guid?>(e, "TenantId") == CurrentTenantId;

		if (existing is null)
		{
			entityBuilder.HasQueryFilter(tenantPredicate);
			return;
		}

		var param = tenantPredicate.Parameters[0];
		var reboundExisting = new ParameterReplacer(existing.Parameters[0], param).Visit(existing.Body)!;
		var combinedBody = Expression.AndAlso(reboundExisting, tenantPredicate.Body);
		var combined = Expression.Lambda<Func<TEntity, bool>>(combinedBody, param);
		entityBuilder.HasQueryFilter(combined);
	}

	private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
	{
		protected override Expression VisitParameter(ParameterExpression node)
			=> node == from ? to : base.VisitParameter(node);
	}
}
