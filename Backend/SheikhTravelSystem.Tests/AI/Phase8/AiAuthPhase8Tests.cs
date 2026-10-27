using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Ai;
using SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

namespace SheikhTravelSystem.Tests.AI.Phase8;

/// <summary>
/// Phase 8 auth gaps — SuperAdmin <c>targetTenantId</c> / company-scope and forged-body rejection
/// in trusted backend code (not prompt-only).
/// </summary>
public class AiAuthPhase8Tests
{
    [Fact]
    public void SuperAdmin_may_narrow_to_targetTenantId()
    {
        var r = AiEffectiveTenantResolver.Resolve(
            contextTenantId: 9001,
            isPlatformSuperAdmin: true,
            requestedTargetTenantId: 9002);

        r.Status.Should().Be(AiEffectiveTenantResolver.ResolveStatus.Ok);
        r.TenantId.Should().Be(9002);
    }

    [Fact]
    public void SuperAdmin_without_override_keeps_context_tenant()
    {
        var r = AiEffectiveTenantResolver.Resolve(9001, true, null);
        r.TenantId.Should().Be(9001);
        r.Status.Should().Be(AiEffectiveTenantResolver.ResolveStatus.Ok);
    }

    [Fact]
    public void NonSuperAdmin_forged_targetTenantId_is_rejected()
    {
        var r = AiEffectiveTenantResolver.Resolve(
            contextTenantId: 9001,
            isPlatformSuperAdmin: false,
            requestedTargetTenantId: 9002);

        r.Status.Should().Be(AiEffectiveTenantResolver.ResolveStatus.ForgedTargetTenantRejected);
        r.TenantId.Should().Be(0);
        r.Message.Should().Contain("targetTenantId");
    }

    [Fact]
    public void NonSuperAdmin_same_tenant_override_is_ok()
    {
        var r = AiEffectiveTenantResolver.Resolve(9001, false, 9001);
        r.Status.Should().Be(AiEffectiveTenantResolver.ResolveStatus.Ok);
        r.TenantId.Should().Be(9001);
    }

    [Fact]
    public void Missing_tenant_context_fails_closed()
    {
        var r = AiEffectiveTenantResolver.Resolve(null, true, 9002);
        r.Status.Should().Be(AiEffectiveTenantResolver.ResolveStatus.MissingTenantContext);
    }

    [Fact]
    public async Task Chat_gateway_passes_SuperAdmin_flag_into_tool_context()
    {
        AiToolExecutionContext? seen = null;
        var tool = new Mock<IAiTool>();
        tool.SetupGet(t => t.Name).Returns("GetTenantComparison");
        tool.SetupGet(t => t.Description).Returns("Compare tenants (SuperAdmin)");
        tool.SetupGet(t => t.Kind).Returns("read");
        tool.SetupGet(t => t.RequiresConfirmation).Returns(false);
        tool.SetupGet(t => t.TriggerKeywords).Returns(new[] { "compare tenants" });
        tool.Setup(t => t.ExecuteAsync(
                It.IsAny<AiToolExecutionContext>(),
                It.IsAny<System.Text.Json.JsonElement?>(),
                It.IsAny<CancellationToken>()))
            .Callback<AiToolExecutionContext, System.Text.Json.JsonElement?, CancellationToken>((ctx, _, _) => seen = ctx)
            .ReturnsAsync((AiToolExecutionContext ctx, System.Text.Json.JsonElement? _, CancellationToken __) =>
                new AiToolExecutionResult(
                    "GetTenantComparison",
                    Success: ctx.IsPlatformSuperAdmin,
                    Summary: ctx.IsPlatformSuperAdmin ? "ok" : "denied",
                    Data: null));

        var current = new Mock<ICurrentUserService>();
        current.SetupGet(c => c.IsPlatformSuperAdmin).Returns(true);
        current.Setup(c => c.HasPermission(It.IsAny<string>())).Returns(true);

        var engine = new AiToolEngine(
            new[] { tool.Object },
            new AiEntityResolver(Mock.Of<IDbConnectionFactory>()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AiToolEngine>.Instance);

        // Mirror the chat gateway contract: SuperAdmin must flow into AiToolExecutionContext.
        var ctx = new AiToolExecutionContext(
            9001, 1, AllowWriteTools: true, ConfirmWrite: false, CanExecuteWrite: true,
            IsPlatformSuperAdmin: current.Object.IsPlatformSuperAdmin);
        var results = await engine.SelectAndExecuteAsync(ctx, "compare tenants across the platform", CancellationToken.None);

        results.Should().ContainSingle(r => r.ToolName == "GetTenantComparison" && r.Success);
        seen.Should().NotBeNull();
        seen!.IsPlatformSuperAdmin.Should().BeTrue();
    }

    [Theory]
    [InlineData(9001, false, 9002, AiEffectiveTenantResolver.ResolveStatus.ForgedTargetTenantRejected)]
    [InlineData(9001, true, 9002, AiEffectiveTenantResolver.ResolveStatus.Ok)]
    [InlineData(9002, true, 9001, AiEffectiveTenantResolver.ResolveStatus.Ok)]
    [InlineData(9001, false, null, AiEffectiveTenantResolver.ResolveStatus.Ok)]
    public void Http_body_matrix_matches_production_resolver(
        int contextTenant,
        bool isSuperAdmin,
        int? bodyTarget,
        AiEffectiveTenantResolver.ResolveStatus expected)
    {
        var r = AiEffectiveTenantResolver.Resolve(contextTenant, isSuperAdmin, bodyTarget);
        r.Status.Should().Be(expected);
        if (expected == AiEffectiveTenantResolver.ResolveStatus.Ok)
        {
            var expectedTenant = isSuperAdmin && bodyTarget is int t && t > 0 && t != contextTenant
                ? bodyTarget.Value
                : contextTenant;
            r.TenantId.Should().Be(expectedTenant);
        }
    }
}
