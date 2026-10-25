using TowerHaat.Domain.Businesses;

namespace TowerHaat.Tests.Domain;

public sealed class BusinessHoursTests
{
    private static TimeOnly T(int hour, int minute = 0) => new(hour, minute);

    [Theory]
    [InlineData(7, 59, false)]
    [InlineData(8, 0, true)]
    [InlineData(20, 59, true)]
    [InlineData(21, 0, false)]
    public void Daytime_hours(int hour, int minute, bool open)
    {
        var hours = new BusinessHours(T(8), T(21), null);

        Assert.Equal(open, hours.Contains(T(hour, minute)));
    }

    [Theory]
    [InlineData(17, 59, false)]
    [InlineData(18, 0, true)]
    [InlineData(23, 30, true)]
    [InlineData(0, 30, true)]
    [InlineData(1, 0, false)]
    public void Hours_past_midnight(int hour, int minute, bool open)
    {
        var hours = new BusinessHours(T(18), T(1), null);

        Assert.Equal(open, hours.Contains(T(hour, minute)));
    }

    [Fact]
    public void Same_opening_and_closing_time_means_all_day()
    {
        Assert.True(new BusinessHours(T(0), T(0), null).Contains(T(3)));
    }

    [Theory]
    [InlineData(19, 0, true)]
    [InlineData(20, 0, true)]
    [InlineData(20, 1, false)]
    public void Orders_stop_at_the_cut_off_even_while_open(int hour, int minute, bool accepts)
    {
        var hours = new BusinessHours(T(8), T(21), T(20));

        Assert.Equal(accepts, hours.AcceptsOrdersAt(T(hour, minute)));
    }

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(0, 15, true)]
    [InlineData(0, 31, false)]
    public void Cut_off_after_midnight(int hour, int minute, bool accepts)
    {
        var hours = new BusinessHours(T(18), T(1), T(0, 30));

        Assert.Equal(accepts, hours.AcceptsOrdersAt(T(hour, minute)));
    }

    [Fact]
    public void No_orders_outside_hours()
    {
        Assert.False(new BusinessHours(T(8), T(21), null).AcceptsOrdersAt(T(22)));
    }
}
