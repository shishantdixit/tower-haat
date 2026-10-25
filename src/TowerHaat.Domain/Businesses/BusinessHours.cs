namespace TowerHaat.Domain.Businesses;

/// <summary>
/// Daily opening hours and the last time orders are taken (B-02), in Indian time.
/// Hours that pass midnight (18:00 to 01:00) are supported.
/// </summary>
public sealed record BusinessHours(TimeOnly OpensAt, TimeOnly ClosesAt, TimeOnly? AcceptOrdersUntil)
{
    public static readonly BusinessHours Default = new(new TimeOnly(8, 0), new TimeOnly(21, 0), null);

    public bool Contains(TimeOnly time) =>
        OpensAt == ClosesAt
        || (OpensAt < ClosesAt ? time >= OpensAt && time < ClosesAt : time >= OpensAt || time < ClosesAt);

    /// <summary>Whether a new order can be placed at this time: open, and not past the cut-off.</summary>
    public bool AcceptsOrdersAt(TimeOnly time)
    {
        if (!Contains(time))
        {
            return false;
        }

        if (AcceptOrdersUntil is not { } until)
        {
            return true;
        }

        // Measure both from opening time so cut-offs after midnight compare correctly.
        return Since(OpensAt, time) <= Since(OpensAt, until);
    }

    private static TimeSpan Since(TimeOnly start, TimeOnly time) =>
        time >= start ? time - start : time - start + TimeSpan.FromDays(1);
}
