using FluentAssertions;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Infrastructure.Services;

namespace SheikhTravelSystem.Tests.CustomerPortal;

public class PortalAuthTests
{
    [Fact]
    public void PortalOtpStore_ValidatesAndConsumesCode()
    {
        var store = new PortalOtpStore(Options.Create(new PortalAuthSettings { OtpExpiryMinutes = 5 }));
        store.Store("+923001234567", "123456");

        store.TryValidate("+923001234567", "123456", out var err1).Should().BeTrue();
        err1.Should().BeNull();

        store.TryValidate("+923001234567", "123456", out var err2).Should().BeFalse();
        err2.Should().NotBeNullOrEmpty();
    }
}
