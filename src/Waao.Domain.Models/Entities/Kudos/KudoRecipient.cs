namespace Waao.Domain.Models.Entities.Kudos;

public class KudoRecipient : Entity
{
	public Guid KudoId { get; set; }
	public virtual Kudo Kudo { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public string CollaboratorName { get; set; } = string.Empty;
	public string? CollaboratorPhotoUrl { get; set; }
}
