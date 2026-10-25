using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TowerHaat.Api.Auth;

namespace TowerHaat.Api.Notifications;

/// <summary>
/// Live order updates for open apps (B-05, C-07). Server to client only: the app refreshes the order
/// through the normal API when it hears "orderChanged", so nothing private travels on the hub.
/// </summary>
[Authorize]
internal sealed class OrdersHub : Hub
{
    public const string Path = "/hubs/orders";
    public const string OrderChanged = "orderChanged";
}

/// <summary>SignalR addresses users by the token's subject, the user id.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst(AppClaims.UserId)?.Value;
}
