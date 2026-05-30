using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Kudos;

public class Kudo : Entity
{
	public Guid GiverId { get; set; }
	public virtual Collaborator Giver { get; set; } = null!;

	public string GiverName { get; set; } = string.Empty;
	public string? GiverPhotoUrl { get; set; }

	public KudoValue Value { get; set; }
	public string Message { get; set; } = string.Empty;

	public virtual ICollection<KudoRecipient> Recipients { get; set; } = [];
}
