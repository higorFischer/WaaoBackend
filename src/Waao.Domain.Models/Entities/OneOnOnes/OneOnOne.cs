using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.OneOnOnes;

public class OneOnOne : Entity
{
	public Guid ManagerId { get; set; }
	public virtual Collaborator Manager { get; set; } = null!;
	public string ManagerName { get; set; } = string.Empty;

	public Guid ReportId { get; set; }
	public virtual Collaborator Report { get; set; } = null!;
	public string ReportName { get; set; } = string.Empty;

	public DateOnly ScheduledDate { get; set; }
	public OneOnOneStatus Status { get; set; } = OneOnOneStatus.Scheduled;

	/// <summary>Shared agenda before the meeting (markdown).</summary>
	public string? Agenda { get; set; }
	/// <summary>Notes captured during/after — visible to both sides (markdown).</summary>
	public string? Notes { get; set; }

	public DateTime? CompletedAt { get; set; }

	public virtual ICollection<OneOnOneActionItem> ActionItems { get; set; } = [];
}

public class OneOnOneActionItem : Entity
{
	public Guid OneOnOneId { get; set; }
	public virtual OneOnOne OneOnOne { get; set; } = null!;

	public string Text { get; set; } = string.Empty;
	public bool IsDone { get; set; }
	public DateTime? DoneAt { get; set; }
	public DateOnly? DueDate { get; set; }

	public Guid? AssignedToId { get; set; }
	public string? AssignedToName { get; set; }

	public int Position { get; set; }
}
