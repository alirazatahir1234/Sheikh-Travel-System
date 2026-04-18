using FluentAssertions;
using SheikhTravelSystem.Domain.Constants;

namespace SheikhTravelSystem.Tests.Common;

public class AppConstantsTests
{
    [Fact]
    public void DefaultPageSize_Is20()
    {
        AppConstants.DefaultPageSize.Should().Be(20);
    }

    [Fact]
    public void MaxPageSize_Is100()
    {
        AppConstants.MaxPageSize.Should().Be(100);
    }

    [Fact]
    public void DefaultCurrency_IsPKR()
    {
        AppConstants.DefaultCurrency.Should().Be("PKR");
    }
}
