using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Hubs;

[Authorize]
public class CallsHub(WaaoDbContext Db, ICallPresenceTracker Presence) : Hub
{
	private const string ListenersGroup = "calls-listeners";

	public override async Task OnConnectedAsync()
	{
		// Everyone subscribes to the global "calls list updated" stream so the
		// sidebar / calls page stays in sync without polling.
		await Groups.AddToGroupAsync(Context.ConnectionId, ListenersGroup);
		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		// Drop the connection from every call channel it was in and tell everyone.
		var affected = Presence.Leave(Context.ConnectionId);
		foreach (var channelId in affected)
			await BroadcastPresenceAsync(channelId);

		await base.OnDisconnectedAsync(exception);
	}

	/// <summary>Frontend calls this when the user enters a voice channel.</summary>
	public async Task JoinCall(Guid channelId)
	{
		var callerId = GetCallerId();
		var me = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == callerId);
		if (me is null) return;

		// If they were in another call, leave it first.
		var previous = Presence.Leave(Context.ConnectionId);

		Presence.Join(channelId, Context.ConnectionId, callerId, me.FullName, me.PhotoUrl);

		foreach (var prev in previous)
			await BroadcastPresenceAsync(prev);
		await BroadcastPresenceAsync(channelId);
	}

	/// <summary>Frontend calls this when the user leaves the voice channel.</summary>
	public async Task LeaveCall()
	{
		var affected = Presence.Leave(Context.ConnectionId);
		foreach (var channelId in affected)
			await BroadcastPresenceAsync(channelId);
	}

	private async Task BroadcastPresenceAsync(Guid channelId)
	{
		var participants = Presence.GetParticipants(channelId);
		await Clients.Group(ListenersGroup).SendAsync("CallPresenceChanged", new
		{
			channelId,
			participants,
		});
	}

	private Guid GetCallerId()
	{
		var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
		          ?? Context.User?.FindFirstValue("sub");
		return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
	}
}
