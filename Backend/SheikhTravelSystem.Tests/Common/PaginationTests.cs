using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Domain.Constants;

namespace SheikhTravelSystem.Tests.Common;

public class PaginationTests
{
    [Fact]
    public void PagedRequest_DefaultValues_AreCorrect()
    {
        var request = new PagedRequest();
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(AppConstants.DefaultPageSize);
    }

    [Fact]
    public void PagedRequest_NegativePage_ClampedTo1()
    {
        var request = new PagedRequest { Page = -5 };
        request.Page.Should().Be(1);
    }

    [Fact]
    public void PagedRequest_ZeroPage_ClampedTo1()
    {
        var request = new PagedRequest { Page = 0 };
        request.Page.Should().Be(1);
    }

    [Fact]
    public void PagedRequest_ExcessivePageSize_ClampedToMax()
    {
        var request = new PagedRequest { PageSize = 999999 };
        request.PageSize.Should().Be(AppConstants.MaxPageSize);
    }

    [Fact]
    public void PagedRequest_NegativePageSize_ResetsToDefault()
    {
        var request = new PagedRequest { PageSize = -1 };
        request.PageSize.Should().Be(AppConstants.DefaultPageSize);
    }

    [Fact]
    public void PagedRequest_ValidPageSize_Accepted()
    {
        var request = new PagedRequest { PageSize = 50 };
        request.PageSize.Should().Be(50);
    }

    [Fact]
    public void PagedRequest_MaxPageSize_Accepted()
    {
        var request = new PagedRequest { PageSize = AppConstants.MaxPageSize };
        request.PageSize.Should().Be(AppConstants.MaxPageSize);
    }

    [Fact]
    public void PagedResult_TotalPages_CalculatedCorrectly()
    {
        var result = new PagedResult<string>
        {
            TotalCount = 55, Page = 1, PageSize = 20,
            Items = new List<string>(new string[20])
        };

        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void PagedResult_LastPage_HasNoNext()
    {
        var result = new PagedResult<string>
        {
            TotalCount = 55, Page = 3, PageSize = 20,
            Items = new List<string>(new string[15])
        };

        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }
}
