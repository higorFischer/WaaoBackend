using Microsoft.AspNetCore.SignalR;
using Waao.API.Hubs;

namespace Waao.Tests.Support;

/// <summary>
/// A no-op IHubContext&lt;MessagingHub&gt; stub for unit tests that don't care about SignalR broadcasts.
/// </summary>
public sealed class NullHubContext : IHubContext<MessagingHub>
{
	public static readonly NullHubContext Instance = new();

	public IHubClients Clients => NullHubClients.Instance;
	public IGroupManager Groups => NullGroupManager.Instance;
}

file sealed class NullHubClients : IHubClients
{
	public static readonly NullHubClients Instance = new();

	public IClientProxy All => NullClientProxy.Instance;
	public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => NullClientProxy.Instance;
	public IClientProxy Client(string connectionId) => NullClientProxy.Instance;
	public IClientProxy Clients(IReadOnlyList<string> connectionIds) => NullClientProxy.Instance;
	public IClientProxy Group(string groupName) => NullClientProxy.Instance;
	public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => NullClientProxy.Instance;
	public IClientProxy Groups(IReadOnlyList<string> groupNames) => NullClientProxy.Instance;
	public IClientProxy User(string userId) => NullClientProxy.Instance;
	public IClientProxy Users(IReadOnlyList<string> userIds) => NullClientProxy.Instance;
}

file sealed class NullClientProxy : IClientProxy
{
	public static readonly NullClientProxy Instance = new();

	public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;
}

file sealed class NullGroupManager : IGroupManager
{
	public static readonly NullGroupManager Instance = new();

	public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;
}
