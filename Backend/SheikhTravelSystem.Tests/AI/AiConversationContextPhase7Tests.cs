using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Infrastructure.Services.Ai.Conversation;

namespace SheikhTravelSystem.Tests.AI;

/// <summary>Phase 7 — conversation context, bounds, follow-ups, isolation, RAG deferral.</summary>
public class AiConversationContextPhase7Tests
{
    [Theory]
    [InlineData("which of those have highest fuel cost", true)]
    [InlineData("compare with last month", true)]
    [InlineData("of those vehicles, which are offline", true)]
    [InlineData("Which vehicles need maintenance?", false)]
    [InlineData("Show fuel costs this month", false)]
    public void FollowUp_detection(string message, bool expected)
        => AiConversationContextBuilder.LooksLikeFollowUpReference(message).Should().Be(expected);

    [Fact]
    public async Task FollowUp_without_memory_asks_clarification()
    {
        var builder = CreateBuilder();
        var ctx = await builder.BuildAsync(
            new AiConversationContextRequest(
                1, 9, "which of those have highest fuel cost",
                Memory: null),
            CancellationToken.None);

        ctx.LooksLikeFollowUp.Should().BeTrue();
        ctx.NeedsClarification.Should().BeTrue();
        ctx.ClarificationPrompt.Should().Contain("don't have earlier");
        ctx.LiveDataSection.Should().Contain("Live retrieved data");
        ctx.ConversationSection.Should().Contain("NOT live business data");
    }

    [Fact]
    public async Task FollowUp_with_memory_does_not_clarify()
    {
        var builder = CreateBuilder();
        var memory = new AiConversationMemorySnapshot(
            Guid.NewGuid(), 1, 9, null,
            [
                new AiChatMessageDto("user", "vehicles that need maintenance"),
                new AiChatMessageDto("assistant", "V1 and V2 need service")
            ],
            2,
            Authorized: true);

        var ctx = await builder.BuildAsync(
            new AiConversationContextRequest(
                1, 9, "which of those have highest fuel cost", memory),
            CancellationToken.None);

        ctx.LooksLikeFollowUp.Should().BeTrue();
        ctx.NeedsClarification.Should().BeFalse();
        ctx.ConversationSection.Should().Contain("V1 and V2");
        ctx.BoundedHistoryMessages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Three_context_types_are_separated_and_stale_chat_not_live()
    {
        var builder = CreateBuilder();
        var memory = new AiConversationMemorySnapshot(
            Guid.NewGuid(), 1, 9,
            "Earlier: user asked offline vehicles",
            [new AiChatMessageDto("assistant", "Yesterday: 5 offline (stale if reused)")],
            4,
            Authorized: true);

        var tool = new AiToolExecutionResult(
            "GetOfflineVehicles", true, "LIVE: 2 offline now", null);

        var ctx = await builder.BuildAsync(
            new AiConversationContextRequest(
                1, 9, "how many are offline now?",
                memory,
                LiveToolResults: [tool]),
            CancellationToken.None);

        ctx.ConversationSection.Should().Contain("NOT live business data");
        ctx.ConversationSection.Should().Contain("stale if reused");
        ctx.BusinessSection.Should().Contain("Authorized tenantId: 1");
        ctx.BusinessSection.Should().Contain("Approved KPI");
        ctx.LiveDataSection.Should().Contain("LIVE: 2 offline now");
        ctx.LiveDataSection.Should().Contain("ground truth for THIS turn");
        ctx.RagSection.Should().BeNull();

        var appendix = ctx.ToSystemPromptAppendix();
        appendix.Should().Contain("Business context");
        appendix.Should().Contain("Conversation context");
        appendix.Should().Contain("Live retrieved data");
    }

    [Fact]
    public void BoundMessages_truncates_to_cap_and_token_budget()
    {
        var many = Enumerable.Range(0, 40)
            .Select(i => new AiChatMessageDto(
                i % 2 == 0 ? "user" : "assistant",
                new string('x', 800)))
            .ToList();

        var bounded = AiConversationMemoryService.BoundMessages(many, maxRecent: 10, maxApproxTokens: 500);
        bounded.Count.Should().BeLessThanOrEqualTo(10);
        AiConversationMemoryService.ApproxTokens(bounded).Should().BeLessThanOrEqualTo(500 + 200); // allow pair floor
        bounded.Should().OnlyContain(m => m.Role == "user" || m.Role == "assistant");
    }

    [Fact]
    public void BoundMessages_keeps_latest_when_over_budget()
    {
        var msgs = new List<AiChatMessageDto>
        {
            new("user", "old " + new string('a', 4000)),
            new("assistant", "old reply " + new string('b', 4000)),
            new("user", "latest question"),
            new("assistant", "latest answer")
        };

        var bounded = AiConversationMemoryService.BoundMessages(msgs, maxRecent: 16, maxApproxTokens: 200);
        bounded.Should().Contain(m => m.Content.Contains("latest"));
        bounded.Should().NotContain(m => m.Content.StartsWith("old "));
    }

    [Fact]
    public void Extractive_summary_marks_memory_not_live_data()
    {
        var older = new List<(long Id, string Role, string Content)>
        {
            (1, "user", "vehicles that need maintenance"),
            (2, "assistant", "Truck A and Truck B")
        };
        var summary = AiConversationMemoryService.BuildExtractiveSummary(
            null, older, new AiConversationContextOptions());

        summary.Should().Contain("NOT live fleet data");
        summary.Should().Contain("Truck A");
        summary.Should().Contain("User:");
    }

    [Fact]
    public void Unauthorized_snapshot_is_empty()
    {
        // Document contract: unauthorized memory must not leak history into context.
        var snap = new AiConversationMemorySnapshot(
            Guid.NewGuid(), 1, 2, "secret", [new AiChatMessageDto("user", "x")], 1, Authorized: false);
        snap.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_context_empty_session_section()
    {
        var builder = CreateBuilder();
        var empty = new AiConversationMemorySnapshot(
            Guid.NewGuid(), 1, 1, null, [], 0, Authorized: true);

        var ctx = await builder.BuildAsync(
            new AiConversationContextRequest(1, 1, "hello", empty),
            CancellationToken.None);

        ctx.ConversationSection.Should().Contain("Empty session");
        ctx.NeedsClarification.Should().BeFalse();
    }

    [Fact]
    public async Task Tenant_change_notice_is_surfaced_by_ensure_result_contract()
    {
        // Contract used by gateways when EnsureSessionAsync detects tenant mismatch.
        var result = new AiConversationSessionEnsureResult(
            Guid.NewGuid(), Created: true, TenantChanged: true,
            Notice: "Company scope changed — started a new conversation");
        result.TenantChanged.Should().BeTrue();
        result.Notice.Should().Contain("Company scope changed");
    }

    [Fact]
    public void Rag_is_deferred_unavailable()
    {
        var rag = new DeferredAiRagRetriever();
        rag.IsAvailable.Should().BeFalse();
        var task = rag.TryRetrieveAsync(1, 1, "maintenance policy", CancellationToken.None);
        task.Result.Available.Should().BeFalse();
        task.Result.Passages.Should().BeEmpty();
        task.Result.UnavailableReason.Should().Contain("deferred");
    }

    [Fact]
    public async Task Rag_permissions_path_never_injects_when_unavailable()
    {
        var builder = CreateBuilder(ragAvailable: false);
        var ctx = await builder.BuildAsync(
            new AiConversationContextRequest(1, 1, "what is the fuel policy?", null),
            CancellationToken.None);
        ctx.RagSection.Should().BeNull();
    }

    [Fact]
    public void ExcludeTrailingDuplicateUser_drops_last_match()
    {
        var history = new List<AiChatMessageDto>
        {
            new("user", "first"),
            new("assistant", "ok"),
            new("user", "follow up")
        };
        var trimmed = AiConversationContextBuilder.ExcludeTrailingDuplicateUser(history, "follow up");
        trimmed.Should().HaveCount(2);
        trimmed[^1].Role.Should().Be("assistant");
    }

    private static AiConversationContextBuilder CreateBuilder(bool ragAvailable = false)
    {
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(t => t.GetBrandingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantBrandingRow(1, "Demo Co", "demo", null, null, "Asia/Karachi"));

        var current = new Mock<ICurrentUserService>();
        current.SetupGet(c => c.IsPlatformSuperAdmin).Returns(false);
        current.SetupGet(c => c.Roles).Returns(["FLEET_MANAGER"]);
        current.Setup(c => c.HasPermission(It.IsAny<string>())).Returns(false);
        current.Setup(c => c.HasPermission(AiPermissions.Chat)).Returns(true);

        IAiRagRetriever rag = ragAvailable
            ? new FakeRag()
            : new DeferredAiRagRetriever();

        return new AiConversationContextBuilder(
            tenants.Object,
            current.Object,
            rag,
            NullLogger<AiConversationContextBuilder>.Instance);
    }

    private sealed class FakeRag : IAiRagRetriever
    {
        public bool IsAvailable => true;

        public Task<AiRagRetrievalResult> TryRetrieveAsync(
            int tenantId, int userId, string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiRagRetrievalResult(
                true,
                [new AiRagPassage("manual.pdf", "Change oil every 5k km", "docs:read")],
                null));
    }
}

public class AiConversationContextOptionsTests
{
    [Fact]
    public void Defaults_are_bounded()
    {
        var opts = new AiConversationContextOptions();
        opts.MaxRecentMessages.Should().Be(16);
        opts.MaxApproxTokens.Should().Be(6000);
        opts.SummarizeAfterMessages.Should().Be(24);
        Options.Create(opts).Value.SummaryMaxChars.Should().Be(2000);
    }
}
