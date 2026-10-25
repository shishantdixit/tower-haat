namespace TowerHaat.Domain.Users;

/// <summary>Indian mobile numbers, stored as +91 followed by 10 digits.</summary>
public static class PhoneNumber
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        var hasPlus = input.TrimStart().StartsWith('+');

        if (digits.Length == 12 && digits.StartsWith("91"))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 11 && digits.StartsWith('0') && !hasPlus)
        {
            digits = digits[1..];
        }
        else if (digits.Length != 10 || hasPlus)
        {
            return false;
        }

        if (digits[0] is < '6' or > '9')
        {
            return false;
        }

        normalized = "+91" + digits;
        return true;
    }
}
