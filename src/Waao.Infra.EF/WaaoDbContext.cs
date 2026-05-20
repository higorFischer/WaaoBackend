using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Kanban;

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

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(WaaoDbContext).Assembly);
	}
}
