using System.Text.RegularExpressions;

namespace TowerHaat.Application.Common;

internal static partial class Validate
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new AppException(ErrorKind.Validation, $"{field} is required (at most {maxLength} characters).");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new AppException(ErrorKind.Validation, $"{field} can be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static decimal Amount(decimal value, string field, decimal max)
    {
        if (value < 0 || value > max || decimal.Round(value, 2) != value)
        {
            throw new AppException(ErrorKind.Validation, $"{field} must be between ₹0 and ₹{max:0} with at most 2 decimals.");
        }

        return value;
    }

    /// <summary>A UPI virtual payment address such as name@bank.</summary>
    public static string? UpiId(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return UpiPattern().IsMatch(trimmed)
            ? trimmed
            : throw new AppException(ErrorKind.Validation, "Enter a valid UPI ID, for example name@okaxis.");
    }

    /// <summary>FSSAI licence and registration numbers have 14 digits.</summary>
    public static string? FssaiNumber(string? value)
    {
        var digits = value?.Replace(" ", string.Empty);
        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }

        return FssaiPattern().IsMatch(digits)
            ? digits
            : throw new AppException(ErrorKind.Validation, "An FSSAI number has 14 digits.");
    }

    /// <summary>UPI transaction reference (UTR), usually 12 digits; optional.</summary>
    public static string? Utr(string? value)
    {
        var trimmed = value?.Replace(" ", string.Empty);
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return UtrPattern().IsMatch(trimmed)
            ? trimmed
            : throw new AppException(ErrorKind.Validation, "The UTR is the 12-digit reference in your UPI app's payment details.");
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]{6,35}$")]
    private static partial Regex UtrPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z][a-zA-Z0-9]{1,63}$")]
    private static partial Regex UpiPattern();

    [GeneratedRegex(@"^\d{14}$")]
    private static partial Regex FssaiPattern();
}
