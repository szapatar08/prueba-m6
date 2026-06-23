using Prueba.Application.Interfaces;

namespace Prueba.Api.Middleware;

public class TenantResolution
{
    private readonly RequestDelegate _next;

    public TenantResolution(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant)
    {
        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            currentTenant.SetTenant(tenantId);
        }

        await _next(context);
    }
}
