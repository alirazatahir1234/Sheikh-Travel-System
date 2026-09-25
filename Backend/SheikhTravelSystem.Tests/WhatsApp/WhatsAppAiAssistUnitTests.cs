using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.AiAssist;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Infrastructure.Services.Ai.Providers;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppAiAssistPromptTests
{
    [Fact]
    public void ParseModelJson_HighConfidence_WhenFactsPresent()
    {
        var raw = """
            {"suggestion":"Your driver Ahmed is en route with Toyota Camry ABC-123.","confidence":"High","confidenceReason":"Trip facts present","unavailableFacts":[]}
            """;
        var parsed = WhatsAppAiAssistPrompt.ParseModelJson(raw, [], emptyConversation: false);
        parsed.Suggestion.Should().Contain("Ahmed");
        parsed.Confidence.Should().Be(WhatsAppAiAssistConfidence.High);
        parsed.ParsedFromJson.Should().BeTrue();
    }

    [Fact]
    public void ParseModelJson_EmptyConversation_ForcesLow()
    {
        var raw = """{"suggestion":"Hello","confidence":"High","unavailableFacts":[]}""";
        var parsed = WhatsAppAiAssistPrompt.ParseModelJson(raw, ["messages"], emptyConversation: true);
        parsed.Confidence.Should().Be(WhatsAppAiAssistConfidence.Low);
    }

    [Fact]
    public void ParseModelJson_MissingRaw_ReturnsFallbackLow()
    {
        var parsed = WhatsAppAiAssistPrompt.ParseModelJson(null, ["active_bookings"], emptyConversation: false);
        parsed.Confidence.Should().Be(WhatsAppAiAssistConfidence.Low);
        parsed.Suggestion.Should().Contain("booking or trip reference");
    }

    [Fact]
    public void BuildFactsBlock_MarksMissingActiveTrip()
    {
        var ctx = new WhatsAppConversationContextDto(
            1, "+971500000000", "Ali", 10, "Ali", "Acme", null, null,
            [], null, 0m);
        var facts = WhatsAppAiAssistPrompt.BuildFactsBlock(ctx, null, [], out var missing);
        facts.Should().Contain("ActiveTrip: NONE");
        missing.Should().Contain("active_trip_driver_vehicle");
        missing.Should().Contain("messages");
    }

    [Fact]
    public void BuildFactsBlock_IncludesDriverVehicleWhenPresent()
    {
        var trip = new WhatsAppAiActiveTripFactsDto(
            9, "TR-1", 5, "BK-1", "Driver En Route", "Ahmed", "Toyota Camry", "ABC-123",
            "https://track.sheikhgo.com/t/tok");
        var msg = new WhatsAppMessageDto(1, 1, "Inbound", null, "text", "Where is my driver?", null, "Delivered", DateTime.UtcNow);
        var facts = WhatsAppAiAssistPrompt.BuildFactsBlock(null, trip, [msg], out _);
        facts.Should().Contain("Ahmed");
        facts.Should().Contain("ABC-123");
        facts.Should().Contain("Where is my driver?");
    }

    [Theory]
    [InlineData("MakeShorter", WhatsAppAiAssistOperations.Shorter)]
    [InlineData("MakeMoreProfessional", WhatsAppAiAssistOperations.Professional)]
    [InlineData("Regenerate", WhatsAppAiAssistOperations.Regenerate)]
    [InlineData("Translate", WhatsAppAiAssistOperations.Translate)]
    public void NormalizeTransformAction(string input, string expected)
        => WhatsAppAiAssistPrompt.NormalizeTransformAction(input).Should().Be(expected);
}

public class WhatsAppAiAssistCommandsTests
{
    [Fact]
    public async Task Suggest_Unauthorized_ReturnsForbidden()
    {
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(WhatsAppPermissions.AiAssist)).Returns(false);

        var handler = new SuggestWhatsAppAiReplyCommandHandler(
            DummyRunner(), tenant.Object, user.Object);
        var result = await handler.Handle(new SuggestWhatsAppAiReplyCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Summary_Unauthorized_ReturnsForbidden()
    {
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(WhatsAppPermissions.AiAssist)).Returns(false);

        var handler = new SummarizeWhatsAppConversationCommandHandler(
            DummyRunner(), tenant.Object, user.Object);
        var result = await handler.Handle(new SummarizeWhatsAppConversationCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("FORBIDDEN");
    }

    private static WhatsAppAiAssistRunner DummyRunner()
        => new(
            Mock.Of<IWhatsAppInboxRepository>(MockBehavior.Strict),
            Mock.Of<IWhatsAppInboxExtended>(MockBehavior.Strict),
            Mock.Of<IWhatsAppAiAssistAuditRepository>(MockBehavior.Strict),
            Mock.Of<IAiProviderResolver>(MockBehavior.Strict),
            Mock.Of<IAiManagementService>(MockBehavior.Strict),
            NullLogger<WhatsAppAiAssistRunner>.Instance);
}

public class WhatsAppAiAssistServiceTests
{
    private readonly Mock<IWhatsAppInboxRepository> _inbox = new();
    private readonly Mock<IWhatsAppInboxExtended> _inboxExtended = new();
    private readonly Mock<IWhatsAppAiAssistAuditRepository> _audit = new();
    private readonly Mock<IAiProviderResolver> _resolver = new();
    private readonly Mock<IAiManagementService> _mgmt = new();
    private readonly Mock<IAiProvider> _provider = new();
    private readonly WhatsAppAiAssistRunner _sut;

    public WhatsAppAiAssistServiceTests()
    {
        _audit.Setup(a => a.InsertAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mgmt.Setup(m => m.RecordUsageAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new WhatsAppAiAssistRunner(
            _inbox.Object,
            _inboxExtended.Object,
            _audit.Object,
            _resolver.Object,
            _mgmt.Object,
            NullLogger<WhatsAppAiAssistRunner>.Instance);
    }

    [Fact]
    public async Task Suggest_WithRichContext_ReturnsHighConfidenceSuggestion()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "Where is my driver?")]);
        SetupContext(withBookings: true);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppAiActiveTripFactsDto(
                9, "TR-9", 5, "BK-5", "Driver En Route", "Ahmed", "Toyota Camry", "ABC-123",
                "https://track.sheikhgo.com/t/x"));
        SetupProviderSuccess("""
            {"suggestion":"Your driver Ahmed is currently en route with Toyota Camry ABC-123. Track: https://track.sheikhgo.com/t/x","confidence":"High","confidenceReason":"Trip facts","unavailableFacts":[]}
            """);

        var result = await _sut.SuggestAsync(1, 7, 42, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Suggestion.Should().Contain("Ahmed");
        result.Data.Confidence.Should().Be(WhatsAppAiAssistConfidence.High);
        result.Data.ReviewRecommended.Should().BeFalse();
        result.Data.Provider.Should().Be("Mock");
        _provider.Verify(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mgmt.Verify(m => m.RecordUsageAsync(1, "whatsapp_assist", "Mock", It.IsAny<int>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Suggest_MissingContext_LowConfidence_DoesNotInventDriver()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "Where is my driver?")]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        SetupProviderSuccess("""
            {"suggestion":"I do not have verified driver details yet. Please share your booking reference.","confidence":"Low","confidenceReason":"No active trip","unavailableFacts":["driver_name","vehicle_info"]}
            """);

        var result = await _sut.SuggestAsync(1, 7, 42, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.ReviewRecommended.Should().BeTrue();
        result.Data.Confidence.Should().Be(WhatsAppAiAssistConfidence.Low);
        result.Data.Suggestion.Should().NotContain("invented");
    }

    [Fact]
    public async Task Suggest_EmptyConversation_SkipsProvider()
    {
        SetupConversation();
        SetupMessages([]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);

        var result = await _sut.SuggestAsync(1, 7, 42, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Confidence.Should().Be(WhatsAppAiAssistConfidence.Low);
        result.Data.Suggestion.Should().Contain("no messages");
        _resolver.Verify(r => r.ResolveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suggest_ProviderFailure_ReturnsError()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "Hello")]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        SetupProvider(new AiProviderChatResult(false, "", "Mock", "mock", Error: "boom"));

        var result = await _sut.SuggestAsync(1, 7, 42, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("PROVIDER_ERROR");
    }

    [Fact]
    public async Task Suggest_ProviderUnavailable_ReturnsError()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "Hello")]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        _resolver.Setup(r => r.ResolveAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new AiProviderConfigDto(Provider: "None", IsEnabled: false)));

        var result = await _sut.SuggestAsync(1, 7, 42, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("PROVIDER_UNAVAILABLE");
    }

    [Fact]
    public async Task Suggest_Timeout_ReturnsTimeoutCode()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "Hello")]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        _resolver.Setup(r => r.ResolveAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((_provider.Object, new AiProviderConfigDto(Provider: "Mock", IsEnabled: true, ModelName: "mock")));
        _provider.Setup(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _sut.SuggestAsync(1, 7, 42, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("TIMEOUT");
    }

    [Fact]
    public async Task Regenerate_CallsProviderWithPriorSuggestion()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "Need airport transfer")]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        AiProviderChatRequest? captured = null;
        SetupProviderSuccess(
            """{"suggestion":"Happy to arrange an airport transfer. What date works?","confidence":"Medium","unavailableFacts":[]}""",
            req => captured = req);

        var result = await _sut.SuggestAsync(
            1, 7, 42,
            new WhatsAppAiAssistSuggestRequest(PriorSuggestion: "We can help with that."),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Operation.Should().Be(WhatsAppAiAssistOperations.Regenerate);
        captured.Should().NotBeNull();
        captured!.Messages.Last().Content.Should().Contain("We can help with that.");
    }

    [Fact]
    public async Task Transform_Shorter_RequiresText()
    {
        var result = await _sut.TransformAsync(
            1, 7, 42,
            new WhatsAppAiAssistTransformRequest("Shorter", Text: null),
            CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Code.Should().Be("AI_ASSIST_TEXT_REQUIRED");
    }

    [Fact]
    public async Task Transform_Professional_Succeeds()
    {
        SetupConversation();
        SetupMessages([Msg(1, "Inbound", "hi")]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        SetupProviderSuccess("""{"suggestion":"Good afternoon. How may we assist you today?","confidence":"High","unavailableFacts":[]}""");

        var result = await _sut.TransformAsync(
            1, 7, 42,
            new WhatsAppAiAssistTransformRequest("Professional", Text: "hey whats up"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Operation.Should().Be(WhatsAppAiAssistOperations.Professional);
        result.Data.Suggestion.Should().Contain("Good afternoon");
    }

    [Fact]
    public async Task Summarize_ReturnsSummary()
    {
        SetupConversation();
        SetupMessages([
            Msg(1, "Inbound", "I want airport transportation"),
            Msg(2, "Outbound", "Sure, from which airport?")
        ]);
        SetupContext(withBookings: false);
        _audit.Setup(a => a.GetLatestActiveTripFactsAsync(1, It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppAiActiveTripFactsDto?)null);
        SetupProviderSuccess("""
            {"suggestion":"Customer wants airport transportation. Destination not selected yet.","confidence":"High","unavailableFacts":[]}
            """);

        var result = await _sut.SummarizeAsync(1, 7, 42, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Operation.Should().Be(WhatsAppAiAssistOperations.Summary);
        result.Data.Suggestion.Should().Contain("airport");
    }

    [Fact]
    public async Task Suggest_ConversationNotFound()
    {
        _inbox.Setup(i => i.GetConversationAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppConversationDto?)null);

        var result = await _sut.SuggestAsync(1, 7, 99, new WhatsAppAiAssistSuggestRequest(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be("NOT_FOUND");
    }

    private void SetupConversation()
    {
        _inbox.Setup(i => i.GetConversationAsync(1, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppConversationDto
            {
                Id = 42,
                AccountId = 1,
                ContactId = 3,
                ContactPhone = "+971500000000",
                ContactName = "Ali",
                CustomerId = 10,
                Status = "Open"
            });
    }

    private void SetupMessages(IReadOnlyList<WhatsAppMessageDto> messages)
    {
        _inbox.Setup(i => i.GetMessagesAsync(1, 42, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((messages, messages.Count));
    }

    private void SetupContext(bool withBookings)
    {
        var bookings = withBookings
            ? new List<WhatsAppContextBookingDto> { new(5, "BK-5", "Confirmed", DateTime.UtcNow.Date) }
            : new List<WhatsAppContextBookingDto>();
        _inboxExtended.Setup(i => i.GetConversationContextAsync(1, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppConversationContextDto(
                42, "+971500000000", "Ali", 10, "Ali", "Acme", null, null, bookings, null, 0m));
    }

    private void SetupProviderSuccess(string content, Action<AiProviderChatRequest>? capture = null)
        => SetupProvider(new AiProviderChatResult(true, content, "Mock", "mock", 10, 20), capture);

    private void SetupProvider(AiProviderChatResult result, Action<AiProviderChatRequest>? capture = null)
    {
        _resolver.Setup(r => r.ResolveAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((_provider.Object, new AiProviderConfigDto(Provider: "Mock", IsEnabled: true, ModelName: "mock")));
        _provider.Setup(p => p.ChatAsync(It.IsAny<AiProviderChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiProviderChatRequest, CancellationToken>((req, _) => capture?.Invoke(req))
            .ReturnsAsync(result);
    }

    private static WhatsAppMessageDto Msg(int id, string direction, string body)
        => new(id, 42, direction, null, "text", body, null, "Delivered", DateTime.UtcNow.AddMinutes(id));
}
