using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Orders;

/// <summary>
/// Checkout (C-06). No preferred day means as soon as possible; otherwise "today" or "tomorrow"
/// with a time "HH:mm" in Indian time. The idempotency key is new for each checkout attempt.
/// </summary>
public sealed record PlaceOrderRequest(
    Guid BusinessId,
    IReadOnlyList<OrderLineRequest> Items,
    string? Note,
    string? PreferredDay,
    string? PreferredTime,
    PaymentMethod PaymentMethod,
    Guid IdempotencyKey);

public sealed record OrderLineRequest(Guid ItemId, int Quantity);

public sealed record ReasonRequest(string? Reason);

public sealed record OrderLineResponse(string Name, decimal Price, int Quantity, bool? IsVeg, decimal LineTotal);

public sealed record OrderStepResponse(OrderStatus Status, string? Reason, DateTime At);

/// <summary>An order as its customer or its business sees it (C-07, B-06).</summary>
public sealed record OrderResponse(
    Guid Id,
    int OrderNo,
    Guid BusinessId,
    string BusinessName,
    string BusinessWhatsAppUrl,
    OrderStatus Status,
    DateTime PlacedAt,
    DateTime? ExpiresAt,
    DateTime? PreferredTime,
    string? Note,
    string CustomerName,
    string CustomerPhone,
    string Tower,
    string Flat,
    IReadOnlyList<OrderLineResponse> Items,
    decimal Subtotal,
    decimal DeliveryCharge,
    decimal Total,
    PaymentMethod PaymentMethod,
    IReadOnlyList<OrderStepResponse> History);

public sealed record OrderSummaryResponse(
    Guid Id,
    int OrderNo,
    Guid BusinessId,
    string BusinessName,
    OrderStatus Status,
    DateTime PlacedAt,
    DateTime? ExpiresAt,
    DateTime? PreferredTime,
    string CustomerName,
    string Tower,
    string Flat,
    string ItemsText,
    decimal Total,
    PaymentMethod PaymentMethod);

/// <summary>Tabs of the owner's dashboard (B-05).</summary>
public enum OwnerOrderTab
{
    New,
    Preparing,
    OutForDelivery,
    Done,
}

public sealed record OwnerOrdersResponse(IReadOnlyDictionary<OwnerOrderTab, int> Counts, IReadOnlyList<OrderSummaryResponse> Orders);

/// <summary>B-07: what to make for today's open orders.</summary>
public sealed record PrepSummaryResponse(int OrdersToPrepare, int OrdersAwaitingAcceptance, IReadOnlyList<PrepLine> Items);

public sealed record PrepLine(string Name, bool? IsVeg, int ToPrepare, int AwaitingAcceptance);
