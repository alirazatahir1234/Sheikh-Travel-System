using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;

namespace SheikhTravelSystem.Tests.WhatsApp;

public class WhatsAppSelfServiceBotTests
{
    private readonly Mock<IWhatsAppSelfServiceRepository> _repo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly WhatsAppSelfServiceBot _bot;

    public WhatsAppSelfServiceBotTests()
    {
        _bot = new WhatsAppSelfServiceBot(
            _repo.Object,
            _mediator.Object,
            Options.Create(new WhatsAppOptions { SelfServiceDefaultRouteId = 1, SelfServiceDefaultAmount = 1500 }),
            NullLogger<WhatsAppSelfServiceBot>.Instance);
    }

    [Fact]
    public async Task Idle_ShowsMainMenu()
    {
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppDemoBotStates.Idle, "hi"));
        reply.Should().NotBeNull();
        reply!.NextState.Should().Be(WhatsAppSelfServiceStates.MainMenu);
        reply.ListRows.Should().HaveCount(4);
        reply.ListRows![0].Id.Should().Be(WhatsAppSelfServiceMenu.Book);
    }

    [Fact]
    public async Task Menu_Book_AsksPickup()
    {
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.MainMenu, "ss_book"));
        reply!.NextState.Should().Be(WhatsAppSelfServiceStates.AwaitBookingPickup);
        reply.TextBody.Should().Contain("pick you up");
    }

    [Fact]
    public async Task Menu_Track_AsksReference()
    {
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.MainMenu, "2"));
        reply!.NextState.Should().Be(WhatsAppSelfServiceStates.AwaitTripRef);
    }

    [Fact]
    public async Task Menu_Invoices_Empty()
    {
        _repo.Setup(r => r.ListInvoicesForPhoneAsync(1, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.MainMenu, "ss_invoices"));
        reply!.TextBody.Should().Contain("could not find invoices");
        reply.NextState.Should().Be(WhatsAppSelfServiceStates.MainMenu);
    }

    [Fact]
    public async Task Menu_Agent_DisablesBot()
    {
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.MainMenu, "ss_agent"));
        reply!.DisableBot.Should().BeTrue();
        reply.NextState.Should().Be(WhatsAppSelfServiceStates.HumanHandoff);
        reply.TextBody.Should().Contain("team member");
    }

    [Fact]
    public async Task MainMenu_Unknown_Reprompts()
    {
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.MainMenu, "banana"));
        reply!.TextBody.Should().Contain("MENU");
        reply.NextState.Should().Be(WhatsAppSelfServiceStates.MainMenu);
    }

    [Fact]
    public async Task TripRef_NotFound_FriendlyDeny()
    {
        _repo.Setup(r => r.FindAuthorizedTripOrBookingAsync(1, It.IsAny<string>(), "X-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppSelfServiceTripSummary?)null);
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.AwaitTripRef, "X-1"));
        reply!.TextBody.Should().Be(WhatsAppSelfServiceCopy.TripNotFound);
    }

    [Fact]
    public async Task TripRef_Authorized_ShowsSummary()
    {
        _repo.Setup(r => r.FindAuthorizedTripOrBookingAsync(1, It.IsAny<string>(), "SG-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSelfServiceTripSummary(
                10, "SG-TRIP-1", 5, "SG-BK-1", "Driver En Route", "Ahmed", "Camry", "ABC-1",
                "https://track.sheikhgo.com/t/abc"));
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.AwaitTripRef, "SG-1"));
        reply!.TextBody.Should().Contain("Driver En Route");
        reply.TextBody.Should().Contain("Ahmed");
        reply.TextBody.Should().Contain("track.sheikhgo.com");
    }

    [Fact]
    public async Task Invoices_ListsUnpaidWithPayButtons()
    {
        _repo.Setup(r => r.ListInvoicesForPhoneAsync(1, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new WhatsAppSelfServiceInvoiceItem(9, "INV-1", DateTime.UtcNow, 850, 0, "PKR"),
                new WhatsAppSelfServiceInvoiceItem(8, "INV-2", DateTime.UtcNow, 420, 420, "PKR")
            ]);
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.MainMenu, "ss_invoices"));
        reply!.NextState.Should().Be(WhatsAppSelfServiceStates.Invoices);
        reply.TextBody.Should().Contain("INV-1");
        reply.ReplyButtons.Should().Contain(b => b.Id == "ss_pay_9");
    }

    [Fact]
    public async Task Pay_Unauthorized_Denied()
    {
        _repo.Setup(r => r.GetAuthorizedInvoiceAsync(1, It.IsAny<string>(), 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatsAppSelfServiceInvoiceItem?)null);
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.Invoices, "ss_pay_99"));
        reply!.TextBody.Should().Contain("could not find invoices");
    }

    [Fact]
    public async Task Pay_CheckoutUrl_Returned()
    {
        _repo.Setup(r => r.GetAuthorizedInvoiceAsync(1, It.IsAny<string>(), 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSelfServiceInvoiceItem(9, "INV-1", DateTime.UtcNow, 850, 0, "PKR"));
        _mediator.Setup(m => m.Send(It.IsAny<CreatePortalPaymentCheckoutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<PortalPaymentCheckoutDto>.SuccessResponse(
                new PortalPaymentCheckoutDto("https://pay.example/x", "sess", "Stripe")));

        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.Invoices, "ss_pay_9"));
        reply!.TextBody.Should().Contain("https://pay.example/x");
        reply.TextBody.Should().Contain("Pay securely");
    }

    [Fact]
    public async Task Booking_PickupThenDestination()
    {
        _repo.Setup(r => r.GetBotSessionJsonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.AwaitBookingPickup, "Dubai Airport"));
        reply!.NextState.Should().Be(WhatsAppSelfServiceStates.AwaitBookingDestination);
        reply.Session!.Pickup.Should().Be("Dubai Airport");
    }

    [Fact]
    public async Task Booking_InvalidPassengers_Reprompts()
    {
        _repo.Setup(r => r.GetBotSessionJsonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"pickup":"A","destination":"B","date":"25 Sep 2026","time":"18:30","vehicleType":"SUV"}""");
        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.AwaitBookingPassengers, "0"));
        reply!.NextState.Should().Be(WhatsAppSelfServiceStates.AwaitBookingPassengers);
    }

    [Fact]
    public async Task Booking_Complete_CallsCreateBookingCommand()
    {
        _repo.Setup(r => r.GetBotSessionJsonAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"pickup":"Airport","destination":"Downtown","date":"25 Dec 2026","time":"18:30","vehicleType":"SUV","passengers":2}""");
        _repo.Setup(r => r.EnsureCustomerIdForPhoneAsync(1, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _repo.Setup(r => r.LinkConversationCustomerAsync(1, 1, 42, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mediator.Setup(m => m.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<int>.SuccessResponse(1001));

        var reply = await _bot.ProcessAsync(Ctx(WhatsAppSelfServiceStates.AwaitBookingNotes, "ss_skip_notes"));
        reply!.TextBody.Should().Contain("Booking Confirmed");
        reply.TextBody.Should().Contain("1001");
        _mediator.Verify(m => m.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Flow_DuplicateSubmission_ReturnsExisting()
    {
        _repo.Setup(r => r.GetBookingIdForFlowSubmissionAsync(1, "wamid.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);
        var reply = await _bot.ProcessAsync(new WhatsAppSelfServiceContext(
            1, 1, "+923001234567", null, WhatsAppSelfServiceStates.MainMenu, true, null, null,
            "wamid.1", """{"pickup":"A","destination":"B","date":"25 Dec 2026","time":"10:00","passengers":1}"""));
        reply!.TextBody.Should().Contain("55");
        _mediator.Verify(m => m.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void MenuMatch_IdsAndNumbers()
    {
        WhatsAppSelfServiceMenu.MatchMenuId("ss_book").Should().Be(WhatsAppSelfServiceMenu.Book);
        WhatsAppSelfServiceMenu.MatchMenuId("4").Should().Be(WhatsAppSelfServiceMenu.Agent);
        WhatsAppSelfServiceMenu.MatchMenuId("Talk to an Agent").Should().Be(WhatsAppSelfServiceMenu.Agent);
    }

    private static WhatsAppSelfServiceContext Ctx(string state, string text)
        => new(1, 1, "+923001234567", "Ali", state, true, text, text);
}
