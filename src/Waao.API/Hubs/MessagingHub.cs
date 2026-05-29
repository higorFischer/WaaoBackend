using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Waao.Infra.EF;

namespace Waao.API.Hubs;

[Authorize]
public class MessagingHub(WaaoDbContext Db) : Hub
{
	public override async Task OnConnectedAsync()
	{
		var callerId = GetCallerId();

		// Add the connection to a SignalR group for each channel the caller is a
		// member of, plus the per-user group for personal notifications. Joins
		// are independent — parallelize so a user in 50 channels does not pay a
		// 50× sequential await tax on every reconnect.
		var channelIds = await Db.ChannelMembers.AsNoTracking()
			.Where(m => m.CollaboratorId == callerId)
			.Select(m => m.ChannelId)
			.ToListAsync();

		var joinTasks = new List<Task>(channelIds.Count + 1)
		{
			Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(callerId)),
		};
		foreach (var channelId in channelIds)
			joinTasks.Add(Groups.AddToGroupAsync(Context.ConnectionId, GroupName(channelId)));
		await Task.WhenAll(joinTasks);

		await base.OnConnectedAsync();
	}

	public async Task JoinChannelGroup(Guid channelId)
	{
		var callerId = GetCallerId();

		var isMember = await Db.ChannelMembers
			.AnyAsync(m => m.ChannelId == channelId && m.CollaboratorId == callerId);

		if (!isMember)
			throw new HubException("You are not a member of this channel.");

		await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(channelId));
	}

	public async Task LeaveChannelGroup(Guid channelId)
		=> await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(channelId));

	/// <summary>Broadcast to everyone else in the channel that the caller is typing.
	/// Clients auto-expire the indicator after a few seconds, so no heartbeat is needed.</summary>
	public async Task Typing(Guid channelId)
	{
		var callerId = GetCallerId();
		var name = await Db.Collaborators.AsNoTracking()
			.Where(c => c.Id == callerId)
			.Select(c => c.FullName)
			.FirstOrDefaultAsync() ?? string.Empty;

		await Clients.OthersInGroup(GroupName(channelId))
			.SendAsync("userTyping", new { channelId, collaboratorId = callerId, name });
	}

	/// <summary>Tell others the caller stopped typing (e.g. on send) so the indicator clears instantly.</summary>
	public async Task StopTyping(Guid channelId)
	{
		var callerId = GetCallerId();
		await Clients.OthersInGroup(GroupName(channelId))
			.SendAsync("userStoppedTyping", new { channelId, collaboratorId = callerId });
	}

	public static string GroupName(Guid channelId) => $"channel:{channelId}";
	public static string UserGroupName(Guid collaboratorId) => $"user:{collaboratorId}";

	private Guid GetCallerId()
	{
		var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
			?? Context.User?.FindFirstValue("sub");

		return Guid.TryParse(sub, out var id)
			? id
			: throw new HubException("Missing subject claim.");
	}
}
