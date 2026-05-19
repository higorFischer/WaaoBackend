namespace Waao.Domain.Models.Entities.Kanban;

public class CardLabel : Entity
{
	public Guid BoardId { get; set; }
	public virtual Board Board { get; set; } = null!;

	public string Name { get; set; } = string.Empty;
	public string ColorHex { get; set; } = "#94a3b8";

	public virtual ICollection<CardLabelMap> Mappings { get; set; } = [];
}

public class CardLabelMap : Entity
{
	public Guid CardId { get; set; }
	public virtual Card Card { get; set; } = null!;

	public Guid LabelId { get; set; }
	public virtual CardLabel Label { get; set; } = null!;
}
