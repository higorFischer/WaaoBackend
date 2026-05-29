namespace Waao.Services.Abstractions.Services;

public interface IPresenceTracker
{
	void SetActive(string connectionId, Guid collaboratorId, Guid channelId);
	void Remove(string connectionId);
	bool IsActive(Guid collaboratorId, Guid channelId);
}
