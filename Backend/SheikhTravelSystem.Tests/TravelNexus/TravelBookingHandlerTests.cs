using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelBookingHandlerTests
{
    private const int Tenant = 9;
    private const int BookingId = 70;
    private const int FlightId = 300;
    private const int HotelBookingId = 400;

    private readonly Mock<ITravelBookingRepository> _bookings = new();
    private readonly Mock<ITravelConfigRepository> _config = new();
    private readonly Mock<ITravelCustomerRepository> _customers = new();
    private readonly Mock<ITravelGroupRepository> _groups = new();
    private readonly Mock<ITravelSupplierRepository> _suppliers = new();
    private readonly Mock<ITravelBranchScopeResolver> _scope = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ICurrentUserService> _user = new();

    public TravelBookingHandlerTests()
    {
        _tenant.Setup(t => t.GetRequiredTenantId()).Returns(Tenant);
        _user.Setup(u => u.UserId).Returns(7);
        _scope.Setup(s => s.ResolveAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(TravelBranchScope.CompanyWide);
        _config.Setup(c => c.GetAirportsAsync(null, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new TravelAirportDto { IataCode = "LHE", IanaTimeZone = "Asia/Karachi" },
            new TravelAirportDto { IataCode = "JED", IanaTimeZone = "Asia/Riyadh" }
        ]);
        _bookings.Setup(b => b.GetEligibleTravelersAsync(Tenant, 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(11, (int?)null), (12, (int?)null)]);
    }

    private TravelBookingGuard Guard => new(_bookings.Object, _scope.Object);

    private void GivenFile(string status = "Open", int? customerId = 5, int? groupId = null)
        => _bookings.Setup(b => b.GetStateAsync(BookingId, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelBookingState(BookingId, "BKG-2027-00001", status, customerId, groupId, null));

    private void GivenFlight(string status, int segments = 1, decimal? sell = 160_000, decimal? cost = 100_000, params TravelFlightPassengerFacts[] passengers)
        => _bookings.Setup(b => b.GetFlightStateAsync(BookingId, FlightId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelFlightState(FlightId, BookingId, status, status == "Requested" ? null : "ABC123", sell, cost, sell is null ? null : "PKR", segments)
                { Passengers = passengers });

    private static SaveTravelFlightDto Flight(params int[] travelers) => new(null, "Outbound", "abc123", null, 160_000, 100_000, "pkr", null,
        [new SaveTravelFlightSegmentDto("PK", "759", "LHE", "JED", new DateTime(2027, 3, 1, 3, 0, 0), new DateTime(2027, 3, 1, 5, 30, 0), "Economy", null, null)],
        travelers.Select(t => new SaveTravelFlightPassengerDto(t, null, "Adult")).ToList());

    // ── Tenant isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_booking_is_not_found_for_every_command()
    {
        // GetStateAsync is tenant-scoped in SQL; a foreign id yields null.
        var handlers = new Func<Task>[]
        {
            () => new UpdateTravelBookingCommandHandler(_bookings.Object, _customers.Object, Guard, _tenant.Object)
                .Handle(new UpdateTravelBookingCommand(BookingId, new UpdateTravelBookingDto("x", null, null, null, null)), default),
            () => new TransitionTravelBookingCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new TransitionTravelBookingCommand(BookingId, new TransitionTravelBookingDto("Complete", null)), default),
            () => new SaveTravelFlightCommandHandler(_bookings.Object, _config.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new SaveTravelFlightCommand(BookingId, null, Flight(11)), default),
            () => new SetTravelTicketsCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new SetTravelTicketsCommand(BookingId, FlightId, new SetTravelTicketsDto([new(1, "2142345678901")])), default),
            () => new CancelTravelHotelBookingCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new CancelTravelHotelBookingCommand(BookingId, HotelBookingId, new CancelTravelServiceDto("x", 0, 0, 0, 0)), default),
            () => new UpdateTravelRefundStatusCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
                .Handle(new UpdateTravelRefundStatusCommand(BookingId, 1, new UpdateTravelRefundStatusDto("Closed", null)), default),
            () => new GetTravelBookingByIdQueryHandler(_bookings.Object, _scope.Object, _tenant.Object, _user.Object)
                .Handle(new GetTravelBookingByIdQuery(BookingId), default),
        };
        foreach (var call in handlers) await call.Should().ThrowAsync<NotFoundException>();
        _bookings.Verify(b => b.GetDetailAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_service_of_another_file_is_not_found()
    {
        GivenFile();
        var call = () => new TransitionTravelFlightCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new TransitionTravelFlightCommand(BookingId, FlightId, new TransitionTravelFlightDto("Confirm", "ABC123", null, null)), default);
        await call.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_refuses_a_customer_outside_the_tenant_or_scope()
    {
        _customers.Setup(c => c.GetByIdAsync(5, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>())).ReturnsAsync((TravelCustomerDto?)null);
        var call = () => new CreateTravelBookingCommandHandler(_bookings.Object, _customers.Object, _groups.Object, _scope.Object, _tenant.Object, _user.Object)
            .Handle(new CreateTravelBookingCommand(new CreateTravelBookingDto(5, null, "Umrah March", null, null, null, null, null)), default);
        await call.Should().ThrowAsync<NotFoundException>();
        _bookings.Verify(b => b.CreateAsync(It.IsAny<int>(), It.IsAny<CreateTravelBookingDto>(), It.IsAny<int?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Group_bookings_inherit_the_group_branch_and_refuse_closed_groups()
    {
        var handler = new CreateTravelBookingCommandHandler(_bookings.Object, _customers.Object, _groups.Object, _scope.Object, _tenant.Object, _user.Object);
        var dto = new CreateTravelBookingDto(null, 60, "Group flights", null, null, null, null, null);
        _groups.Setup(g => g.GetStateAsync(60, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelGroupState(60, "Closed", 5, 40, new DateTime(2027, 3, 1), new DateTime(2027, 3, 15), null, 3, "G"));
        (await handler.Handle(new CreateTravelBookingCommand(dto), default)).Success.Should().BeFalse();

        _groups.Setup(g => g.GetStateAsync(60, Tenant, It.IsAny<TravelBranchScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelGroupState(60, "Open", 5, 40, new DateTime(2027, 3, 1), new DateTime(2027, 3, 15), null, 3, "G"));
        _bookings.Setup(b => b.CreateAsync(Tenant, It.Is<CreateTravelBookingDto>(d => d.BranchId == 3 && d.GroupId == 60), 7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingId);
        var ok = await handler.Handle(new CreateTravelBookingCommand(dto), default);
        ok.Success.Should().BeTrue();
        ok.Data.Should().Be(BookingId);
    }

    // ── Flights ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Passengers_must_belong_to_the_booking_customer()
    {
        GivenFile();
        var result = await new SaveTravelFlightCommandHandler(_bookings.Object, _config.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SaveTravelFlightCommand(BookingId, null, Flight(11, 99)), default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("travelers of the booking's customer");
    }

    [Fact]
    public async Task A_new_flight_is_saved_with_normalised_values_and_utc_segments()
    {
        GivenFile();
        _bookings.Setup(b => b.SaveFlightAsync(Tenant, BookingId, null,
                It.Is<SaveTravelFlightDto>(d => d.Pnr == "ABC123" && d.CurrencyCode == "PKR"),
                It.Is<IReadOnlyList<TravelFlightSegmentWrite>>(s => s.Count == 1 && s[0].DepartUtc == new DateTime(2027, 2, 28, 22, 0, 0)),
                true, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FlightId);
        var result = await new SaveTravelFlightCommandHandler(_bookings.Object, _config.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SaveTravelFlightCommand(BookingId, null, Flight(11, 12)), default);
        result.Success.Should().BeTrue(result.Message);
        result.Data.Should().Be(FlightId);
    }

    [Fact]
    public async Task A_PNR_used_by_another_flight_is_refused()
    {
        GivenFile();
        _bookings.Setup(b => b.PnrInUseAsync(Tenant, "ABC123", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new SaveTravelFlightCommandHandler(_bookings.Object, _config.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SaveTravelFlightCommand(BookingId, null, Flight(11)), default);
        result.Message.Should().Contain("already used");
    }

    [Fact]
    public async Task Ticketed_flight_passenger_list_cannot_change()
    {
        GivenFile();
        GivenFlight("Ticketed", 1, 160_000, 100_000, new TravelFlightPassengerFacts(1, 11, null, "Issued", "2142345678901"));
        var result = await new SaveTravelFlightCommandHandler(_bookings.Object, _config.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SaveTravelFlightCommand(BookingId, FlightId, Flight(11, 12)), default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot change once the flight is ticketed");
    }

    [Fact]
    public async Task Ticketing_waits_for_every_ticket_number()
    {
        GivenFile();
        GivenFlight("Confirmed", 1, 160_000, 100_000,
            new TravelFlightPassengerFacts(1, 11, null, "Issued", "2142345678901"), new TravelFlightPassengerFacts(2, 12, null, "NotIssued", null));
        var handler = new TransitionTravelFlightCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object);
        var result = await handler.Handle(new TransitionTravelFlightCommand(BookingId, FlightId, new TransitionTravelFlightDto("Ticket", null, null, null)), default);
        result.Message.Should().Contain("1 passenger(s) have no issued ticket");

        GivenFlight("Confirmed", 0, 160_000, 100_000, new TravelFlightPassengerFacts(1, 11, null, "Issued", "2142345678901"));
        (await handler.Handle(new TransitionTravelFlightCommand(BookingId, FlightId, new TransitionTravelFlightDto("Ticket", null, null, null)), default))
            .Message.Should().Contain("segments");
    }

    [Fact]
    public async Task A_hold_needs_a_future_time_limit()
    {
        GivenFile();
        GivenFlight("Requested");
        var result = await new TransitionTravelFlightCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new TransitionTravelFlightCommand(BookingId, FlightId, new TransitionTravelFlightDto("Hold", "ABC123", DateTime.UtcNow.AddHours(-1), null)), default);
        result.Message.Should().Contain("future");
    }

    [Fact]
    public async Task A_ticket_number_used_elsewhere_is_refused()
    {
        GivenFile();
        GivenFlight("Confirmed", 1, 160_000, 100_000, new TravelFlightPassengerFacts(1, 11, null, "NotIssued", null));
        _bookings.Setup(b => b.TicketNumbersInUseAsync(Tenant, It.IsAny<IReadOnlyList<string>>(), FlightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["2142345678901"]);
        var result = await new SetTravelTicketsCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SetTravelTicketsCommand(BookingId, FlightId, new SetTravelTicketsDto([new(1, "214-2345678901")])), default);
        result.Message.Should().Contain("already used");
    }

    [Fact]
    public async Task Cancelling_a_passenger_records_the_refund_as_pending()
    {
        GivenFile();
        GivenFlight("Ticketed", 1, 160_000, 100_000, new TravelFlightPassengerFacts(1, 11, null, "Issued", "2142345678901"));
        var handler = new CancelTravelFlightPassengerCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object);

        var tooMuch = await handler.Handle(new CancelTravelFlightPassengerCommand(BookingId, FlightId, 1,
            new CancelTravelServiceDto("Illness", 0, 5_000, 0, 200_000, "Refunded")), default);
        tooMuch.Message.Should().Contain("cannot exceed the sell amount");

        _bookings.Setup(b => b.CancelPassengerAsync(Tenant, BookingId, FlightId, 1, "Refunded",
                It.Is<TravelServiceCancellationWrite>(c => c.RefundStatus == "Pending" && c.CurrencyCode == "PKR" && c.RefundToCustomer == 20_000),
                7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);
        var ok = await handler.Handle(new CancelTravelFlightPassengerCommand(BookingId, FlightId, 1,
            new CancelTravelServiceDto("Illness", 2_000, 5_000, 30_000, 20_000, "Refunded")), default);
        ok.Success.Should().BeTrue(ok.Message);
        ok.Data.Should().Be(55);
    }

    [Fact]
    public async Task Cancelling_a_flight_with_issued_tickets_needs_a_ticket_outcome()
    {
        GivenFile();
        GivenFlight("Ticketed", 1, 160_000, 100_000, new TravelFlightPassengerFacts(1, 11, null, "Issued", "2142345678901"));
        var result = await new CancelTravelFlightCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new CancelTravelFlightCommand(BookingId, FlightId, new CancelTravelServiceDto("Schedule change", 0, 0, 0, 0)), default);
        result.Message.Should().Contain("voided or refunded");
    }

    [Fact]
    public async Task Closed_files_refuse_service_changes()
    {
        GivenFile("Completed");
        var result = await new SaveTravelFlightCommandHandler(_bookings.Object, _config.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SaveTravelFlightCommand(BookingId, null, Flight(11)), default);
        result.Message.Should().Be(TravelBookingGuard.NotOpenMessage);
        _bookings.Verify(b => b.SaveFlightAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<SaveTravelFlightDto>(),
            It.IsAny<IReadOnlyList<TravelFlightSegmentWrite>>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Hotels ─────────────────────────────────────────────────────────────

    private static SaveTravelHotelBookingDto Hotel(int? groupHotelId = null, params int[] guests) => new(1, null, groupHotelId,
        new DateTime(2027, 3, 1), new DateTime(2027, 3, 11), "Breakfast", null, null, 4_000, 3_200, "SAR", [new("Double", 2)], guests);

    [Fact]
    public async Task A_customer_file_cannot_link_a_group_stay()
    {
        GivenFile();
        _bookings.Setup(b => b.HotelExistsAsync(Tenant, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await new SaveTravelHotelBookingCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object)
            .Handle(new SaveTravelHotelBookingCommand(BookingId, null, Hotel(groupHotelId: 77, 11)), default);
        result.Message.Should().Contain("does not belong to this booking's group");
    }

    [Fact]
    public async Task Group_file_hotels_take_pilgrims_and_the_groups_own_stay()
    {
        GivenFile(customerId: null, groupId: 60);
        _bookings.Setup(b => b.HotelExistsAsync(Tenant, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _bookings.Setup(b => b.GroupStayExistsAsync(Tenant, 60, 77, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _bookings.Setup(b => b.GetEligibleTravelersAsync(Tenant, null, 60, It.IsAny<CancellationToken>())).ReturnsAsync([(21, (int?)501)]);
        _bookings.Setup(b => b.SaveHotelAsync(Tenant, BookingId, null, It.IsAny<SaveTravelHotelBookingDto>(), 7, It.IsAny<CancellationToken>())).ReturnsAsync(HotelBookingId);
        var handler = new SaveTravelHotelBookingCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object);

        (await handler.Handle(new SaveTravelHotelBookingCommand(BookingId, null, Hotel(77, 21, 22)), default))
            .Message.Should().Contain("active pilgrims of the group");
        (await handler.Handle(new SaveTravelHotelBookingCommand(BookingId, null, Hotel(77, 21)), default)).Data.Should().Be(HotelBookingId);
    }

    [Fact]
    public async Task Hotel_confirmation_needs_the_confirmation_number_and_check_in_waits_for_the_date()
    {
        GivenFile();
        _bookings.Setup(b => b.GetHotelStateAsync(BookingId, HotelBookingId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelHotelBookingState(HotelBookingId, BookingId, "Requested", DateTime.UtcNow.Date.AddDays(10), DateTime.UtcNow.Date.AddDays(20), null, null, null, null));
        var handler = new TransitionTravelHotelBookingCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object);
        (await handler.Handle(new TransitionTravelHotelBookingCommand(BookingId, HotelBookingId, new TransitionTravelHotelDto("Confirm", " ", null)), default))
            .Message.Should().Contain("confirmation number");

        _bookings.Setup(b => b.GetHotelStateAsync(BookingId, HotelBookingId, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelHotelBookingState(HotelBookingId, BookingId, "Confirmed", DateTime.UtcNow.Date.AddDays(10), DateTime.UtcNow.Date.AddDays(20), null, null, null, null));
        (await handler.Handle(new TransitionTravelHotelBookingCommand(BookingId, HotelBookingId, new TransitionTravelHotelDto("CheckIn", null, null)), default))
            .Message.Should().Contain("The stay starts on");
    }

    // ── Refunds, list, suppliers ───────────────────────────────────────────

    [Fact]
    public async Task Refund_status_cannot_skip_backwards()
    {
        GivenFile("Completed");
        _bookings.Setup(b => b.GetCancellationAsync(BookingId, 5, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelCancellationState(5, BookingId, "PaidToCustomer"));
        var handler = new UpdateTravelRefundStatusCommandHandler(_bookings.Object, Guard, _tenant.Object, _user.Object);
        (await handler.Handle(new UpdateTravelRefundStatusCommand(BookingId, 5, new UpdateTravelRefundStatusDto("Pending", null)), default)).Success.Should().BeFalse();

        _bookings.Setup(b => b.UpdateRefundStatusAsync(Tenant, BookingId, 5, "PaidToCustomer", "Closed", null, 7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await handler.Handle(new UpdateTravelRefundStatusCommand(BookingId, 5, new UpdateTravelRefundStatusDto("Closed", null)), default)).Success.Should().BeTrue();
    }

    [Fact]
    public async Task An_inverted_travel_date_range_returns_nothing()
    {
        var result = await new GetTravelBookingsQueryHandler(_bookings.Object, _scope.Object, _tenant.Object)
            .Handle(new GetTravelBookingsQuery { TravelFrom = new DateTime(2027, 3, 10), TravelTo = new DateTime(2027, 3, 1) }, default);
        result.Data!.Items.Should().BeEmpty();
        _bookings.Verify(b => b.GetPagedAsync(It.IsAny<int>(), It.IsAny<TravelBranchScope>(), It.IsAny<TravelBookingListFilter>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_travel_date_range_reaches_the_repository()
    {
        _bookings.Setup(b => b.GetPagedAsync(Tenant, It.IsAny<TravelBranchScope>(),
                It.Is<TravelBookingListFilter>(f => f.TravelFrom == new DateTime(2027, 3, 1) && f.TravelTo == new DateTime(2027, 3, 31) && f.TicketingDueBeforeUtc != null),
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SheikhTravelSystem.Application.Common.PagedResult<TravelBookingListItemDto> { Items = [], Page = 1, PageSize = 20 });
        var result = await new GetTravelBookingsQueryHandler(_bookings.Object, _scope.Object, _tenant.Object)
            .Handle(new GetTravelBookingsQuery { TravelFrom = new DateTime(2027, 3, 1, 15, 0, 0), TravelTo = new DateTime(2027, 3, 31), TicketingDue = true, PageSize = 20 }, default);
        result.Success.Should().BeTrue();
        _bookings.Verify(b => b.GetPagedAsync(Tenant, It.IsAny<TravelBranchScope>(),
            It.Is<TravelBookingListFilter>(f => f.TravelFrom == new DateTime(2027, 3, 1) && f.TravelTo == new DateTime(2027, 3, 31) && f.TicketingDueBeforeUtc != null),
            1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_supplier_in_use_is_not_deleted()
    {
        _suppliers.Setup(s => s.GetByIdAsync(3, Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TravelSupplierDto { Id = 3, UsageCount = 2 });
        var result = await new DeleteTravelSupplierCommandHandler(_suppliers.Object, _tenant.Object).Handle(new DeleteTravelSupplierCommand(3), default);
        result.Message.Should().Contain("deactivate");
        _suppliers.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        var missing = () => new DeleteTravelSupplierCommandHandler(_suppliers.Object, _tenant.Object).Handle(new DeleteTravelSupplierCommand(4), default);
        await missing.Should().ThrowAsync<NotFoundException>();
    }
}
