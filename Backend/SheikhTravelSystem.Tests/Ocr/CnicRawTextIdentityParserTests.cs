using SheikhTravelSystem.Infrastructure.Services.Ocr;
using Xunit;

namespace SheikhTravelSystem.Tests.Ocr;

public class CnicRawTextIdentityParserTests
{
    [Fact]
    public void GuessFatherNameFromRaw_UrduFatherLabel_DoesNotThrow()
    {
        const string raw = "مستقل پتہ محلہ نمبردار والد کا نام محمد حنیف موجودہ پتہ";
        var father = CnicRawTextIdentityParser.GuessFatherNameFromRaw(raw);
        Assert.NotNull(father);
        Assert.Contains("محمد", father, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractPermanentUrduForTranslation_MustaqilBlock_ReturnsUrduSegment()
    {
        const string raw = "مستقل پتہ محلہ اندرون بیروٹ ضلع سیالکوٹ موجودہ پتہ محلہ دیگر";
        var perm = CnicRawTextIdentityParser.ExtractPermanentUrduForTranslation(raw);
        Assert.NotNull(perm);
        Assert.Contains("بیروٹ", perm, StringComparison.Ordinal);
        Assert.DoesNotContain("موجودہ", perm, StringComparison.Ordinal);
    }

    [Fact]
    public void GuessLatinNameFromRaw_NameBeforeFather_ReturnsHolder()
    {
        const string raw = "Name Muhammad Fahim Father Name Muhammad Hanif Gender M";
        var name = CnicRawTextIdentityParser.GuessLatinNameFromRaw(raw);
        Assert.Equal("Muhammad Fahim", name);
    }
}
