using TowerHaat.Domain.Users;

namespace TowerHaat.Tests.Domain;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("9876543210")]
    [InlineData("98765 43210")]
    [InlineData("+91 98765 43210")]
    [InlineData("+91-9876543210")]
    [InlineData("919876543210")]
    [InlineData("09876543210")]
    public void Indian_mobile_numbers_normalize_to_plus_91(string input)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal("+919876543210", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("5876543210")]
    [InlineData("98765432101")]
    [InlineData("+1 9876543210")]
    [InlineData("+44 7911 123456")]
    public void Other_input_is_rejected(string? input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out _));
    }
}
