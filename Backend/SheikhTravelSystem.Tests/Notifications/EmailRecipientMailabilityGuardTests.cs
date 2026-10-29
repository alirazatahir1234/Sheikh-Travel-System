using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Infrastructure.Services.Notifications;

namespace SheikhTravelSystem.Tests.Notifications;

public class EmailRecipientMailabilityGuardTests
{
    [Theory]
    [InlineData("user@sheikhtravel.com", "sheikhtravel.com")]
    [InlineData("User@Gmail.COM.", "gmail.com")]
    [InlineData("a.b+tag@sub.example.org", "sub.example.org")]
    public void TryExtractDomain_parses_common_addresses(string email, string expected)
    {
        EmailRecipientMailabilityGuard.TryExtractDomain(email, out var domain).Should().BeTrue();
        domain.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("@nodomain")]
    [InlineData("user@")]
    [InlineData("user@[127.0.0.1]")]
    public void TryExtractDomain_rejects_invalid(string email)
    {
        EmailRecipientMailabilityGuard.TryExtractDomain(email, out _).Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_skips_when_only_null_mx_or_empty()
    {
        var dns = CreateDns(new MxDnsLookupResult(MxDnsOutcome.Success, []));
        var guard = CreateGuard(dns.Object);

        var decision = await guard.EvaluateAsync("ops@sheikhtravel.com");

        decision.ShouldSend.Should().BeFalse();
        decision.Domain.Should().Be("sheikhtravel.com");
        decision.Reason.Should().Contain("No usable MX");
    }

    [Fact]
    public async Task EvaluateAsync_skips_on_nxdomain()
    {
        var dns = CreateDns(new MxDnsLookupResult(MxDnsOutcome.NxDomain, []));
        var guard = CreateGuard(dns.Object);

        var decision = await guard.EvaluateAsync("x@missing-domain.invalid");

        decision.ShouldSend.Should().BeFalse();
        decision.Reason.Should().Contain("NXDOMAIN");
    }

    [Fact]
    public async Task EvaluateAsync_allows_when_usable_mx_present()
    {
        var dns = CreateDns(new MxDnsLookupResult(
            MxDnsOutcome.Success,
            ["gmail-smtp-in.l.google.com"]));
        var guard = CreateGuard(dns.Object);

        var decision = await guard.EvaluateAsync("user@gmail.com");

        decision.ShouldSend.Should().BeTrue();
        decision.IsTransientDnsUncertainty.Should().BeFalse();
        decision.Reason.Should().Contain("Usable MX");
    }

    [Fact]
    public async Task EvaluateAsync_allows_on_transient_failure()
    {
        var dns = CreateDns(new MxDnsLookupResult(
            MxDnsOutcome.TransientFailure,
            [],
            "ServerFailure"));
        var guard = CreateGuard(dns.Object);

        var decision = await guard.EvaluateAsync("user@flaky.example");

        decision.ShouldSend.Should().BeTrue();
        decision.IsTransientDnsUncertainty.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_caches_negative_result()
    {
        var dns = CreateDns(new MxDnsLookupResult(MxDnsOutcome.Success, []));
        var guard = CreateGuard(dns.Object);

        await guard.EvaluateAsync("a@once.example");
        await guard.EvaluateAsync("b@once.example");

        dns.Verify(
            c => c.LookupMxAsync("once.example", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_rejects_unparseable_email_without_dns()
    {
        var dns = new Mock<IMxDnsLookup>(MockBehavior.Strict);
        var guard = CreateGuard(dns.Object);

        var decision = await guard.EvaluateAsync("not-an-email");

        decision.ShouldSend.Should().BeFalse();
        decision.Reason.Should().Contain("Invalid");
    }

    private static EmailRecipientMailabilityGuard CreateGuard(IMxDnsLookup dns)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Email:MxLookup:CacheMinutes"] = "10",
                ["Notifications:Email:MxLookup:TransientCacheSeconds"] = "120"
            })
            .Build();

        return new EmailRecipientMailabilityGuard(dns, config, NullLogger<EmailRecipientMailabilityGuard>.Instance);
    }

    private static Mock<IMxDnsLookup> CreateDns(MxDnsLookupResult result)
    {
        var dns = new Mock<IMxDnsLookup>();
        dns.Setup(c => c.LookupMxAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return dns;
    }
}
