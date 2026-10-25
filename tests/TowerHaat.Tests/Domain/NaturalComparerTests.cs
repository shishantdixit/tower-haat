using TowerHaat.Application.Common;

namespace TowerHaat.Tests.Domain;

public sealed class NaturalComparerTests
{
    [Fact]
    public void Numbers_inside_names_sort_by_value()
    {
        string[] flats = ["A-1001", "A-201", "a-3", "B-2", "A-10", "A-2"];

        var sorted = flats.OrderBy(f => f, NaturalComparer.Instance).ToArray();

        Assert.Equal(["A-2", "a-3", "A-10", "A-201", "A-1001", "B-2"], sorted);
    }
}
