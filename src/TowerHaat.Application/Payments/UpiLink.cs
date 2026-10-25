using System.Globalization;

namespace TowerHaat.Application.Payments;

/// <summary>
/// The standard UPI deep link (upi://pay) that opens GPay, PhonePe, Paytm and other UPI apps with
/// the business's UPI ID, name and amount filled in. The same text is shown as a QR code.
/// No transaction reference is added: apps reject it for personal UPI IDs.
/// </summary>
public static class UpiLink
{
    public static string Build(string upiId, string payeeName, decimal amount, string note) =>
        "upi://pay"
        + $"?pa={Uri.EscapeDataString(upiId)}"
        + $"&pn={Uri.EscapeDataString(payeeName)}"
        + $"&am={amount.ToString("0.00", CultureInfo.InvariantCulture)}"
        + "&cu=INR"
        + $"&tn={Uri.EscapeDataString(note)}";
}
