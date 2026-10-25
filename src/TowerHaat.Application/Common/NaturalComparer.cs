namespace TowerHaat.Application.Common;

/// <summary>Orders "A-2" before "A-10" by comparing digit runs as numbers.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var startI = i;
                var startJ = j;
                while (i < x.Length && char.IsAsciiDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsAsciiDigit(y[j]))
                {
                    j++;
                }

                var a = x[startI..i].TrimStart('0');
                var b = y[startJ..j].TrimStart('0');
                var byNumber = a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
                if (byNumber != 0)
                {
                    return byNumber;
                }
            }
            else
            {
                var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (byChar != 0)
                {
                    return byChar;
                }

                i++;
                j++;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
