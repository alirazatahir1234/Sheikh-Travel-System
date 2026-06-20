using FluentAssertions;
using FluentValidation;
using SheikhTravelSystem.Application.Common.IO;

namespace SheikhTravelSystem.Tests.Common;

public class MaxLengthReadStreamTests
{
    [Fact]
    public async Task Read_UnderLimit_ShouldSucceed()
    {
        var source = new MemoryStream(new byte[1024]);
        await using var stream = new MaxLengthReadStream(source, 2048);
        var buffer = new byte[1024];

        var read = await stream.ReadAsync(buffer);

        read.Should().Be(1024);
    }

    [Fact]
    public async Task Read_ExceedsLimit_ShouldThrowValidationException()
    {
        var source = new MemoryStream(new byte[2048]);
        await using var stream = new MaxLengthReadStream(source, 1024);
        var buffer = new byte[2048];

        var act = async () => await stream.ReadAsync(buffer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*maximum size*");
    }
}
