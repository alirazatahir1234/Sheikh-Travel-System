using FluentAssertions;
using SheikhTravelSystem.Application.Features.Vehicles;

namespace SheikhTravelSystem.Tests.Vehicles;

public class PlateNumberNormalizerTests
{
    [Theory]
    [InlineData("BWY 217", "BWY-217")]
    [InlineData("LEC-8825", "LEC-8825")]
    [InlineData("  lec  8825 ", "LEC-8825")]
    [InlineData("lea - 19 - 1234", "LEA-19-1234")]
    [InlineData("ABC_123", "ABC-123")]
    [InlineData("ABC–123", "ABC-123")] // en dash
    [InlineData("-ABC-123-", "ABC-123")]
    [InlineData("ABC123", "ABC123")]
    public void Normalize_UsesOneFormat(string input, string expected) =>
        PlateNumberNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_SpaceAndHyphenForms_AreTheSamePlate() =>
        PlateNumberNormalizer.Normalize("BWY 217").Should().Be(PlateNumberNormalizer.Normalize("bwy-217"));

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Normalize_LeavesBlankInputForTheValidator(string? input, string expected) =>
        PlateNumberNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_NeverLengthensThePlate()
    {
        // The validator checks MaximumLength(20) on the raw value; normalising must not push past it.
        const string raw = "ab  cd -- ef __ gh 12";
        PlateNumberNormalizer.Normalize(raw).Length.Should().BeLessThanOrEqualTo(raw.Length);
    }
}
