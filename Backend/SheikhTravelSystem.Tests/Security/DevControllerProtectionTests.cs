using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.API.Controllers;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Tests.Security;

public class DevControllerProtectionTests
{
    [Fact]
    public void DevController_has_Authorize_and_SecurityManage_at_class_level()
    {
        var type = typeof(DevController);
        var authorizeAttrs = type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        authorizeAttrs.Should().NotBeEmpty();
        authorizeAttrs.Should().Contain(a =>
            a is RequirePermissionAttribute
            && a.Policy == PlatformPermissions.SecurityManage);

        // Plain [Authorize] (or RequirePermission which is an AuthorizeAttribute) gates anonymous callers.
        authorizeAttrs.Should().Contain(a => a.GetType() == typeof(AuthorizeAttribute)
            || a is RequirePermissionAttribute);
    }

    [Fact]
    public async Task Reseed_outside_Development_returns_NotFound_and_does_not_touch_seeder()
    {
        var seeder = new Mock<IDatabaseSeeder>(MockBehavior.Strict);
        var sut = CreateSut(seeder.Object, Environments.Production);

        var result = await sut.Reseed(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        seeder.Verify(s => s.ResetAndSeedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetAdmin_outside_Development_returns_NotFound_and_does_not_touch_devData()
    {
        var seeder = new Mock<IDatabaseSeeder>(MockBehavior.Strict);
        var devData = new Mock<IDevDataService>(MockBehavior.Strict);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = CreateSut(seeder.Object, Environments.Staging, devData.Object, hasher.Object);

        var result = await sut.ResetAdmin(CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        devData.Verify(
            d => d.ResetAdminPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Seed_in_Development_invokes_seeder()
    {
        var seeder = new Mock<IDatabaseSeeder>();
        seeder.Setup(s => s.SeedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut(seeder.Object, Environments.Development);

        var result = await sut.Seed(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        seeder.Verify(s => s.SeedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DevController CreateSut(
        IDatabaseSeeder seeder,
        string environmentName,
        IDevDataService? devData = null,
        IPasswordHasher? hasher = null)
    {
        return new DevController(
            seeder,
            devData ?? new Mock<IDevDataService>().Object,
            hasher ?? new Mock<IPasswordHasher>().Object,
            new StubHostEnvironment(environmentName));
    }

    private sealed class StubHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "/";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public class LookupControllerProtectionTests
{
    [Fact]
    public void LookupController_is_AllowAnonymous_and_rate_limited_public()
    {
        var type = typeof(LookupController);

        type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().NotBeNull();

        var rateLimit = type.GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true);
        rateLimit.Should().NotBeNull();
        rateLimit!.PolicyName.Should().Be("public");
    }

    [Fact]
    public void GetCurrencies_returns_static_data_without_auth()
    {
        var sut = new LookupController();
        var result = sut.GetCurrencies();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetCountries_returns_static_data_without_auth()
    {
        var sut = new LookupController();
        var result = sut.GetCountries();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }
}
