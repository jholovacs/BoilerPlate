using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BoilerPlate.Diagnostics.WebApi.Hubs;

/// <summary>
///     SignalR hub for real-time event log streaming.
///     Only Service Administrator and Tenant Administrator can connect.
///     Clients are assigned to groups for tenant-scoped filtering.
/// </summary>
[Authorize(Policy = "DiagnosticsODataAccessPolicy")]
public class EventLogsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Service Administrator") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, "service-admins");
        if (Context.User != null)
        {
            var tenantClaim = Context.User.FindFirst("tenant_id")?.Value
                             ?? Context.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
            if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var tenantId))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        }
        await base.OnConnectedAsync();
    }
}
