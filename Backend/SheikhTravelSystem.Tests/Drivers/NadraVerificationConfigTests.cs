using FluentAssertions;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Services.Drivers;

namespace SheikhTravelSystem.Tests.Drivers;

public class NadraVerificationConfigTests
{
    [Fact]
    public void DescribeMissingConfiguration_WhenDisabled_ListsEnabledKey()
    {
        var client = new NadraCnicVerificationClient(
            new FakeHttpClientFactory(),
            Options.Create(new DriverVerificationOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NadraCnicVerificationClient>.Instance);

        var missing = client.DescribeMissingConfiguration();
        missing.Should().Contain(m => m.Contains("Enabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DescribeMissingConfiguration_NishanChannel_RequiresBaseUrlAndApiKey()
    {
        var client = new NadraCnicVerificationClient(
            new FakeHttpClientFactory(),
            Options.Create(new DriverVerificationOptions
            {
                Nadra = new NadraVerificationOptions { Enabled = true, Channel = "Nishan" }
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NadraCnicVerificationClient>.Instance);

        var missing = client.DescribeMissingConfiguration();
        missing.Should().Contain(m => m.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase));
        missing.Should().Contain(m => m.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
