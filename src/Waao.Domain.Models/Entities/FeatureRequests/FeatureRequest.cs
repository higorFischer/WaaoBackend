using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.FeatureRequests;

public class FeatureRequest : Entity
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;

	public FeatureRequestStatus Status { get; set; } = FeatureRequestStatus.New;

	/// <summary>Admin response shown back to the submitter.</summary>
	public string? AdminResponse { get; set; }

	public Guid SubmittedById { get; set; }
	public virtual Collaborator SubmittedBy { get; set; } = null!;

	public virtual ICollection<FeatureRequestVote> Votes { get; set; } = [];

	public virtual ICollection<FeatureRequestComment> Comments { get; set; } = [];
}

public class FeatureRequestVote : Entity
{
	public Guid FeatureRequestId { get; set; }
	public virtual FeatureRequest FeatureRequest { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;
}

public class FeatureRequestComment : Entity
{
	public Guid FeatureRequestId { get; set; }
	public virtual FeatureRequest FeatureRequest { get; set; } = null!;

	public Guid AuthorId { get; set; }
	public virtual Collaborator Author { get; set; } = null!;

	public string Body { get; set; } = string.Empty;
}
