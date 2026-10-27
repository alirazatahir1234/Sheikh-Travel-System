using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Services.Ai.Mcp;

namespace SheikhTravelSystem.Tests.AI;

public class McpToolExecutionLoggerTests
{
    [Fact]
    public void Sanitize_redacts_passwords_tokens_emails()
    {
        var raw =
            "Password=AliSheikh@123 Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.aaa.bbb " +
            "user@sheikhgo.com refresh_token=xyzSECRET";
        var outText = McpToolExecutionLogger.Sanitize(raw) ?? string.Empty;
        outText.Should().NotContain("AliSheikh@123");
        outText.Should().NotContain("eyJhbGciOi");
        outText.Should().NotContain("user@sheikhgo.com");
        outText.Should().NotContain("xyzSECRET");
        (outText.Contains("[REDACTED]") || outText.Contains("[EMAIL]")).Should().BeTrue();
    }

    [Fact]
    public void SanitizeArgsPreview_redacts_secret_keys_and_truncates()
    {
        var preview = McpToolExecutionLogger.SanitizeArgsPreview(
            new Dictionary<string, object?>
            {
                ["tenantId"] = 7,
                ["password"] = "super-secret",
                ["sql"] = "SELECT 1 WHERE TenantId = @TenantId"
            });

        preview.Should().Contain("tenantId");
        preview.Should().Contain("[REDACTED]");
        preview.Should().NotContain("super-secret");
    }

    [Fact]
    public void SanitizeArgsPreview_empty_args()
    {
        McpToolExecutionLogger.SanitizeArgsPreview(null).Should().Be("{}");
        McpToolExecutionLogger.SanitizeArgsPreview(new Dictionary<string, object?>())
            .Should().Be("{}");
    }
}
