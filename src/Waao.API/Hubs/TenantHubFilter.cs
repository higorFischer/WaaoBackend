using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Waao.Services.Abstractions.Services;
using Waao.Services.Tenancy;

namespace Waao.API.Hubs;

/// <summary>
/// Resolves the caller's tenant for every SignalR hub connection and invocation.
///
/// Each hub method runs in its own DI scope, so we must set the scoped
/// <see cref="ITenantContext"/> per call (the HTTP middleware doesn't run for
/// SignalR-over-WebSocket invocations after the initial upgrade). Reads
/// 'tenant_id' from the JWT and falls back to WAAO so existing tokens keep
/// working — same fallback policy as <c>TenantResolutionMiddleware</c>.
/// </summary>
public sealed class TenantHubFilter : IHubFilter
{
	public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
	{
		ResolveTenant(context.Context.User, context.ServiceProvider);
		await next(context);
	}

	public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
	{
		ResolveTenant(context.Context.User, context.ServiceProvider);
		await next(context, exception);
	}

	public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
	{
		ResolveTenant(invocationContext.Context.User, invocationContext.ServiceProvider);
		return next(invocationContext);
	}

	private static void ResolveTenant(ClaimsPrincipal? user, IServiceProvider sp)
	{
		var ctx = sp.GetService<ITenantContext>();
		if (ctx is null) return;

		var claim = user?.FindFirstValue("tenant_id");
		var tenantId = Guid.TryParse(claim, out var parsed) ? parsed : TenantConstants.WaaoTenantId;
		ctx.SetTenant(tenantId);
	}
}
