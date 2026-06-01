using System.Security.Claims;
using Waao.Services.Abstractions.Services;
using Waao.Services.Tenancy;

namespace Waao.API.Middleware;

/// <summary>
/// Populates <see cref="ITenantContext"/> from the JWT 'tenant_id' claim on every
/// authenticated request. Tokens issued before multi-tenancy lack this claim — in
/// that case we fall back to the WAAO default so legacy clients keep working until
/// they refresh their token.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate Next)
{
	public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
	{
		var user = context.User;
		if (user.Identity?.IsAuthenticated == true)
		{
			var claim = user.FindFirstValue("tenant_id");
			if (Guid.TryParse(claim, out var tenantId))
				tenantContext.SetTenant(tenantId);
			else
				tenantContext.SetTenant(TenantConstants.WaaoTenantId);
		}

		await Next(context);
	}
}
