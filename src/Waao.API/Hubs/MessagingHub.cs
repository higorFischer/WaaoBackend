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

		// Add the connection to a SignalR group for each channel the caller is a member of
		var channelIds = await Db.ChannelMembers
			.Where(m => m.CollaboratorId == callerId)
			.Select(m => m.ChannelId)
			.ToListAsync();

		foreach (var channelId in channelIds)
			await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(channelId));

		// Add the connection to the per-user group for personal notifications
		await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(callerId));

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
