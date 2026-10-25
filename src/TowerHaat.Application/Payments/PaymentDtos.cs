using TowerHaat.Domain.Orders;

namespace TowerHaat.Application.Payments;

public sealed record PaymentResponse(
    PaymentStatus Status,
    string? Utr,
    DateTime? MarkedPaidAt,
    DateTime? SettledAt,
    DateTime? RefundedAt,
    string? Note,
    UpiPaymentResponse? Upi);

/// <summary>Shown only while the order can be paid by UPI: after acceptance and until it is paid (C-08).</summary>
public sealed record UpiPaymentResponse(string UpiId, string PayeeName, decimal Amount, string Link);

public sealed record MarkPaidRequest(string? Utr);

public enum PaymentAction
{
    Confirm,
    Collect,
    Dispute,
    Refund,
}

public sealed record PaymentActionRequest(PaymentAction Action, string? Note);
