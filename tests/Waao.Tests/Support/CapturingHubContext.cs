using Microsoft.AspNetCore.SignalR;
using Waao.API.Hubs;

namespace Waao.Tests.Support;

/// <summary>
/// A capturing IHubContext&lt;MessagingHub&gt; stub that records SendCoreAsync calls for assertions.
/// </summary>
public sealed class CapturingHubContext : IHubContext<MessagingHub>
{
	public IHubClients Clients { get; } = new CapturingHubClients();
	public IGroupManager Groups => NullGroupManager.InstanceInternal;

	public IReadOnlyList<(string Group, string Method, object?[] Args)> Calls
		=> ((CapturingHubClients)Clients).Calls;
}

file sealed class CapturingHubClients : IHubClients
{
	private readonly List<(string Group, string Method, object?[] Args)> _calls = [];
	public IReadOnlyList<(string Group, string Method, object?[] Args)> Calls => _calls;

	public IClientProxy All => new CapturingClientProxy("*", _calls);
	public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => NullClientProxy.InstanceInternal;
	public IClientProxy Client(string connectionId) => NullClientProxy.InstanceInternal;
	public IClientProxy Clients(IReadOnlyList<string> connectionIds) => NullClientProxy.InstanceInternal;
	public IClientProxy Group(string groupName) => new CapturingClientProxy(groupName, _calls);
	public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => NullClientProxy.InstanceInternal;
	public IClientProxy Groups(IReadOnlyList<string> groupNames) => NullClientProxy.InstanceInternal;
	public IClientProxy User(string userId) => NullClientProxy.InstanceInternal;
	public IClientProxy Users(IReadOnlyList<string> userIds) => NullClientProxy.InstanceInternal;
}

file sealed class CapturingClientProxy(string group, List<(string Group, string Method, object?[] Args)> calls) : IClientProxy
{
	public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
	{
		calls.Add((group, method, args));
		return Task.CompletedTask;
	}
}

file sealed class NullClientProxy : IClientProxy
{
	public static readonly NullClientProxy InstanceInternal = new();
	public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;
}

file sealed class NullGroupManager : IGroupManager
{
	public static readonly NullGroupManager InstanceInternal = new();
	public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
	public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
