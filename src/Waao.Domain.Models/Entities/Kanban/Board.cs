using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Kanban;

public class Board : Entity
{
	public string Slug { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#2A6B7E";

	public BoardVisibility Visibility { get; set; } = BoardVisibility.Team;

	public Guid OwnerId { get; set; }
	public virtual Collaborator Owner { get; set; } = null!;

	public bool IsArchived { get; set; }

	public virtual ICollection<BoardMember> Members  { get; set; } = [];
	public virtual ICollection<BoardColumn> Columns  { get; set; } = [];
	public virtual ICollection<Epic> Epics           { get; set; } = [];
	public virtual ICollection<CardLabel> Labels     { get; set; } = [];
}

public class BoardMember : Entity
{
	public Guid BoardId { get; set; }
	public virtual Board Board { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public BoardMemberRole Role { get; set; } = BoardMemberRole.Editor;
}
