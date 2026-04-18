using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class ApiResponseTests
{
    [Fact]
    public void SuccessResponse_SetsCorrectProperties()
    {
        var result = ApiResponse<string>.SuccessResponse("data", "ok");
        result.Success.Should().BeTrue();
        result.Data.Should().Be("data");
        result.Message.Should().Be("ok");
        result.Errors.Should().BeNull();
    }

    [Fact]
    public void FailResponse_SetsCorrectProperties()
    {
        var errors = new List<string> { "err1", "err2" };
        var result = ApiResponse<string>.FailResponse("fail", errors);
        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Be("fail");
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void FailResponse_WithoutErrors_HasNullErrorsList()
    {
        var result = ApiResponse<int>.FailResponse("fail");
        result.Success.Should().BeFalse();
        result.Errors.Should().BeNull();
    }
}
