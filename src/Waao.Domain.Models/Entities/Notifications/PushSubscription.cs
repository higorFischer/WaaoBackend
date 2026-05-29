namespace Waao.Domain.Models.Entities.Notifications;

public class PushSubscription : Entity
{
	public Guid CollaboratorId { get; set; }
	public string Endpoint { get; set; } = string.Empty;
	public string P256dh { get; set; } = string.Empty;
	public string Auth { get; set; } = string.Empty;
	public string? UserAgent { get; set; }
}
