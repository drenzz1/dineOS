using DineOS.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace DineOS.Api.Hubs;

/*
 * ─── Frontend Integration ─────────────────────────────────────────────────────
 *
 * Hub URL : /hubs/orders
 * Auth    : JWT passed as query string (WebSocket cannot set headers in browsers):
 *             ?access_token=<keycloak_access_token>
 *
 * Connect (TypeScript / @microsoft/signalr):
 *
 *   import { HubConnectionBuilder } from "@microsoft/signalr";
 *
 *   const connection = new HubConnectionBuilder()
 *     .withUrl("/hubs/orders", { accessTokenFactory: () => getAccessToken() })
 *     .withAutomaticReconnect()
 *     .build();
 *
 *   // Event: new order placed
 *   connection.on("OrderCreated", (evt: {
 *     orderId:     number;
 *     tenantId:    number;
 *     orderType:   string;           // "dine-in" | "pickup"
 *     tableNumber: number | null;
 *     status:      string;           // "New"
 *     total:       number;
 *     notes:       string | null;
 *     createdAt:   string;           // ISO 8601
 *     items: {
 *       id: number; name: string; quantity: number;
 *       unitPrice: number; notes: string | null;
 *     }[];
 *   }) => { ... });
 *
 *   // Event: order status updated
 *   connection.on("OrderStatusChanged", (evt: {
 *     orderId:   number;
 *     tenantId:  number;
 *     oldStatus: string;             // e.g. "New"
 *     newStatus: string;             // e.g. "InProgress"
 *     changedAt: string;             // ISO 8601
 *   }) => { ... });
 *
 *   await connection.start();
 *
 * Notes:
 *   - Each connection is automatically scoped to the caller's tenant via the
 *     `tenant_id` JWT claim. Clients receive ONLY their own restaurant's events.
 *   - SuperAdmin connections are authenticated but not placed in any tenant group,
 *     so they receive no order events (by design).
 * ──────────────────────────────────────────────────────────────────────────────
 */
[Authorize]
public class OrderUpdatesHub : Hub<IOrderClient>
{
    public static string GroupName(long tenantId) => $"tenant-{tenantId}";

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirstValue("tenant_id");
        if (tenantId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(long.Parse(tenantId)));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirstValue("tenant_id");
        if (tenantId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(long.Parse(tenantId)));

        await base.OnDisconnectedAsync(exception);
    }
}
