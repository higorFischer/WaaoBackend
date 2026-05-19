namespace Waao.Domain.Models.Enums;

public enum BoardVisibility
{
	Private = 0,    // owner only
	Team    = 1,    // explicit members
	Public  = 2,    // all collaborators read
}

public enum BoardMemberRole
{
	Viewer    = 0,
	Commenter = 1,
	Editor    = 2,
	Owner     = 3,
}

public enum CardPriority
{
	Low      = 0,
	Medium   = 1,
	High     = 2,
	Critical = 3,
}

public enum CardActivityKind
{
	Created          = 0,
	Moved            = 1,
	Updated          = 2,
	Assigned         = 3,
	Unassigned       = 4,
	Commented        = 5,
	LabelAdded       = 6,
	LabelRemoved     = 7,
	Archived         = 8,
	Restored         = 9,
	ChecklistChanged = 10,
}
