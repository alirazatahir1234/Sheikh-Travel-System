using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common.Configuration;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Infrastructure.Services;

namespace SheikhTravelSystem.Tests.CustomerPortal;

public class PortalAuthTests
{
    [Fact]
    public void PortalOtpStore_ValidatesAndConsumesCode()
    {
        var store = new PortalOtpStore(Options.Create(new PortalAuthSettings { OtpExpiryMinutes = 5 }));
        store.Store("+923001234567", "654321");

        store.TryValidate("+923001234567", "654321", out var err1).Should().BeTrue();
        err1.Should().BeNull();

        store.TryValidate("+923001234567", "654321", out var err2).Should().BeFalse();
        err2.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendPortalOtp_DevelopmentDevMode_UsesFixedCodeAndSkipsSms()
    {
        var otp = new Mock<IPortalOtpService>();
        var sms = new Mock<ISmsOtpService>();
        string? stored = null;
        otp.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, code) => stored = code);

        var handler = CreateHandler(
            otp.Object,
            sms.Object,
            isDevelopment: true,
            config: new Dictionary<string, string?>
            {
                ["PortalAuth:DevMode"] = "true",
                ["PortalAuth:DevOtpCode"] = "654321"
            });

        var result = await handler.Handle(new SendPortalOtpCommand("+923001234567"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.DevMode.Should().BeTrue();
        stored.Should().Be("654321");
        sms.Verify(
            x => x.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendPortalOtp_ProductionIgnoresDevMode_SendsSms()
    {
        var otp = new Mock<IPortalOtpService>();
        var sms = new Mock<ISmsOtpService>();
        string? stored = null;
        otp.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, code) => stored = code);

        var handler = CreateHandler(
            otp.Object,
            sms.Object,
            isDevelopment: false,
            environmentName: "Production",
            config: new Dictionary<string, string?>
            {
                ["PortalAuth:DevMode"] = "true",
                ["PortalAuth:DevOtpCode"] = "654321"
            });

        var result = await handler.Handle(new SendPortalOtpCommand("+923001234567"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.DevMode.Should().BeFalse();
        stored.Should().NotBeNullOrEmpty();
        stored.Should().NotBe("654321");
        stored!.Length.Should().Be(6);
        sms.Verify(
            x => x.SendOtpAsync("+923001234567", stored!, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SendPortalOtpCommandHandler CreateHandler(
        IPortalOtpService otp,
        ISmsOtpService sms,
        bool isDevelopment,
        Dictionary<string, string?> config,
        string environmentName = "Development")
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(isDevelopment ? Environments.Development : environmentName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config!).Build();
        return new SendPortalOtpCommandHandler(
            otp,
            sms,
            configuration,
            env.Object,
            NullLogger<SendPortalOtpCommandHandler>.Instance);
    }
}

public class ProductionSecretsValidatorTests
{
    [Fact]
    public void CollectFailures_SkipsDevelopment()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var env = MockEnv(Environments.Development);
        ProductionSecretsValidator.CollectFailures(config, env).Should().BeEmpty();
    }

    [Fact]
    public void CollectFailures_ProductionReportsMissingKeys_WithoutSecretValues()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ProductionSecretsValidator.Placeholder,
            ["JwtSettings:Secret"] = ProductionSecretsValidator.Placeholder,
            ["Traccar:Enabled"] = "true",
            ["Traccar:Password"] = "",
            ["Geocoding:GoogleMapsApiKey"] = ""
        }).Build();

        var failures = ProductionSecretsValidator.CollectFailures(config, MockEnv(Environments.Production));

        failures.Should().Contain("ConnectionStrings:DefaultConnection");
        failures.Should().Contain("JwtSettings:Secret");
        failures.Should().Contain("Traccar:Password");
        failures.Should().Contain("GoogleMaps:ApiKey or Geocoding:GoogleMapsApiKey");
        // Failure messages must be key names only — never echo configured secret material.
        string.Join(" ", failures).Should().NotContain(ProductionSecretsValidator.Placeholder);
        string.Join(" ", failures).Should().NotContain("AIza");
    }

    [Fact]
    public void CollectFailures_ProductionPassesWhenConfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=x;Trusted_Connection=True;TrustServerCertificate=True;",
            ["JwtSettings:Secret"] = "unit-test-secret-key-at-least-32-chars!!",
            ["Traccar:Enabled"] = "true",
            ["Traccar:Password"] = "unit-test-traccar-password",
            ["GoogleMaps:ApiKey"] = "unit-test-maps-key-not-a-real-credential"
        }).Build();

        ProductionSecretsValidator.CollectFailures(config, MockEnv(Environments.Production)).Should().BeEmpty();
    }

    [Fact]
    public void EnsureValidOrThrow_ThrowsWithKeyNamesOnly()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var act = () => ProductionSecretsValidator.EnsureValidOrThrow(config, MockEnv(Environments.Production));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings:DefaultConnection*")
            .And.Message.Should().NotContain("AIza");
    }

    private static IHostEnvironment MockEnv(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }
}
