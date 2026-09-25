using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Services.WhatsApp;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppWebhookSignatureTests
{
    private const string HmacFixture = "wa-unit-hmac-fixture";

    [Fact]
    public void IsValid_AcceptsMatchingSha256()
    {
        const string payload = """{"object":"whatsapp_business_account"}""";
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(HmacFixture));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var header = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        WhatsAppWebhookSignature.IsValid(HmacFixture, payload, header).Should().BeTrue();
    }

    [Fact]
    public void IsValid_RejectsTamperedPayload()
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(HmacFixture));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("original"));
        var header = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        WhatsAppWebhookSignature.IsValid(HmacFixture, "tampered", header).Should().BeFalse();
    }

    [Fact]
    public void IsValid_RejectsMissingSecretOrHeader()
    {
        WhatsAppWebhookSignature.IsValid(null, "{}", "sha256=abc").Should().BeFalse();
        WhatsAppWebhookSignature.IsValid(HmacFixture, "{}", null).Should().BeFalse();
        WhatsAppWebhookSignature.IsValid(HmacFixture, "{}", "md5=abc").Should().BeFalse();
    }
}

public class WhatsAppPhoneTests
{
    [Theory]
    [InlineData("+971557701219", "971557701219")]
    [InlineData("92 317 7368305", "923177368305")]
    public void ToApiDigits_StripsNonDigits(string input, string expected)
    {
        Application.Features.WhatsApp.WhatsAppPhone.ToApiDigits(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("+971501234567", "UAE")]
    [InlineData("+923001234567", "PK")]
    [InlineData("+966501234567", "UAE")]
    public void ResolveAccountCodeForRecipient_UsesCountry(string phone, string code)
    {
        Application.Features.WhatsApp.WhatsAppPhone.ResolveAccountCodeForRecipient(phone).Should().Be(code);
    }
}
