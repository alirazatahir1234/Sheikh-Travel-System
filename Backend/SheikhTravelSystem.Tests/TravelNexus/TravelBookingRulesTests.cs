using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;
using F = SheikhTravelSystem.Application.Features.TravelNexus.Bookings.TravelFlightStateMachine;
using H = SheikhTravelSystem.Application.Features.TravelNexus.Bookings.TravelHotelStateMachine;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelBookingRulesTests
{
    // ── State machines ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Open", "Complete", "Completed")]
    [InlineData("Open", "Cancel", "Cancelled")]
    [InlineData("Completed", "Reopen", "Open")]
    [InlineData("Completed", "Cancel", null)]
    [InlineData("Cancelled", "Reopen", null)]
    [InlineData("Open", "Reopen", null)]
    public void Booking_file_transitions(string from, string action, string? expected)
        => TravelBookingStateMachine.Target(from, action).Should().Be(expected);

    [Fact]
    public void Only_open_files_allow_service_changes()
    {
        TravelBookingStateMachine.AllowedActions("Open").Should().Contain(["Edit", "ManageServices", "Complete", "Cancel"]);
        TravelBookingStateMachine.AllowedActions("Completed").Should().Equal("Reopen");
        TravelBookingStateMachine.AllowedActions("Cancelled").Should().BeEmpty();
        F.AllowedActions("Requested", fileOpen: false).Should().BeEmpty();
        H.AllowedActions("Confirmed", fileOpen: false, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Requested", "Hold", "OnHold")]
    [InlineData("OnHold", "Hold", "OnHold")]
    [InlineData("Requested", "Confirm", "Confirmed")]
    [InlineData("OnHold", "Confirm", "Confirmed")]
    [InlineData("Confirmed", "Ticket", "Ticketed")]
    [InlineData("Requested", "Ticket", null)]
    [InlineData("Ticketed", "Confirm", null)]
    [InlineData("Ticketed", "Hold", null)]
    [InlineData("Cancelled", "Confirm", null)]
    public void Flight_transitions(string from, string action, string? expected) => F.Target(from, action).Should().Be(expected);

    [Fact]
    public void Ticketed_flights_keep_their_passengers()
    {
        F.CanEditPassengers("Confirmed").Should().BeTrue();
        F.CanEditPassengers("Ticketed").Should().BeFalse();
        F.CanEdit("Ticketed").Should().BeTrue(); // schedule changes
        F.CanEdit("Cancelled").Should().BeFalse();
        F.AllowedActions("Ticketed", true).Should().Contain(["CancelPassenger", "IssueTickets", "Cancel"]).And.NotContain("EditPassengers");
        F.AllowedActions("Requested", true).Should().Contain("Delete");
    }

    [Theory]
    [InlineData("Requested", "Confirm", "Confirmed")]
    [InlineData("Confirmed", "CheckIn", "CheckedIn")]
    [InlineData("CheckedIn", "CheckOut", "CheckedOut")]
    [InlineData("Confirmed", "NoShow", "NoShow")]
    [InlineData("Requested", "CheckIn", null)]
    [InlineData("CheckedOut", "CheckIn", null)]
    [InlineData("NoShow", "CheckIn", null)]
    public void Hotel_transitions(string from, string action, string? expected) => H.Target(from, action).Should().Be(expected);

    [Fact]
    public void Check_in_and_no_show_wait_for_the_check_in_date()
    {
        var checkIn = new DateTime(2027, 3, 1);
        H.AllowedActions("Confirmed", true, checkIn, checkIn.AddDays(-1)).Should().NotContain(["CheckIn", "NoShow"]);
        H.AllowedActions("Confirmed", true, checkIn, checkIn).Should().Contain(["CheckIn", "NoShow"]);
        H.CanCancel("CheckedIn").Should().BeFalse();
    }

    [Fact]
    public void Refund_status_moves_forward_only()
    {
        TravelRefundStateMachine.Initial(0, 0).Should().Be("NotApplicable");
        TravelRefundStateMachine.Initial(0, 100).Should().Be("Pending");
        TravelRefundStateMachine.Next("Pending").Should().Equal("ReceivedFromSupplier", "PaidToCustomer", "Closed");
        TravelRefundStateMachine.Next("ReceivedFromSupplier").Should().Equal("PaidToCustomer", "Closed");
        TravelRefundStateMachine.Next("PaidToCustomer").Should().Equal("Closed");
        TravelRefundStateMachine.Next("Closed").Should().BeEmpty();
        TravelRefundStateMachine.Next("NotApplicable").Should().BeEmpty();
        TravelValues.RefundStatuses.Should().Contain(["Pending", "ReceivedFromSupplier", "PaidToCustomer", "Closed", "NotApplicable"]);
    }

    // ── Times ──────────────────────────────────────────────────────────────

    [Fact]
    public void Every_seeded_airport_has_a_resolvable_time_zone_and_unique_code()
    {
        TravelAirportCatalog.All.Select(a => a.Code).Should().OnlyHaveUniqueItems();
        foreach (var a in TravelAirportCatalog.All)
        {
            a.Code.Should().MatchRegex("^[A-Z]{3}$");
            TravelFlightTimes.FindZone(a.TimeZone).Should().NotBeNull($"{a.Code} uses {a.TimeZone}");
        }
    }

    [Fact]
    public void Local_flight_times_convert_with_the_airport_zone()
    {
        var jeddah = TravelFlightTimes.FindZone("Asia/Riyadh")!;
        TravelFlightTimes.ToUtc(new DateTime(2027, 3, 1, 3, 0, 0), jeddah).Should().Be(new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var london = TravelFlightTimes.FindZone("Europe/London")!;
        TravelFlightTimes.ToUtc(new DateTime(2027, 1, 15, 10, 0, 0), london).Should().Be(new DateTime(2027, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        TravelFlightTimes.ToUtc(new DateTime(2027, 7, 15, 10, 0, 0), london).Should().Be(new DateTime(2027, 7, 15, 9, 0, 0, DateTimeKind.Utc));
        // 28 Mar 2027 01:30 does not exist in London (clocks jump 01:00 → 02:00).
        TravelFlightTimes.ToUtc(new DateTime(2027, 3, 28, 1, 30, 0), london).Should().BeNull();
        // 31 Oct 2027 01:30 happens twice; the earlier (BST) instant is used.
        TravelFlightTimes.ToUtc(new DateTime(2027, 10, 31, 1, 30, 0), london).Should().Be(new DateTime(2027, 10, 31, 0, 30, 0, DateTimeKind.Utc));
    }

    private static readonly TravelAirportDto[] Airports =
    [
        new() { IataCode = "LHE", City = "Lahore", IanaTimeZone = "Asia/Karachi" },
        new() { IataCode = "JED", City = "Jeddah", IanaTimeZone = "Asia/Riyadh" },
        new() { IataCode = "MED", City = "Madinah", IanaTimeZone = "Asia/Riyadh" },
        new() { IataCode = "LHR", City = "London", IanaTimeZone = "Europe/London" },
    ];

    private static SaveTravelFlightSegmentDto Seg(string from, string to, DateTime depart, DateTime arrive)
        => new("PK", "759", from, to, depart, arrive, "Economy", null, null);

    [Fact]
    public void Segment_planner_converts_and_orders_segments()
    {
        // LHE 03:00 (UTC+5) → JED 05:30 (UTC+3): 22:00 → 02:30 UTC, 4h30 flight.
        var (segments, error) = TravelFlightPlanner.Plan(
            [Seg("LHE", "JED", new DateTime(2027, 3, 1, 3, 0, 0), new DateTime(2027, 3, 1, 5, 30, 0))], Airports);
        error.Should().BeNull();
        segments[0].DepartUtc.Should().Be(new DateTime(2027, 2, 28, 22, 0, 0));
        segments[0].ArriveUtc.Should().Be(new DateTime(2027, 3, 1, 2, 30, 0));
        segments[0].Sequence.Should().Be(1);
    }

    [Fact]
    public void Segment_planner_refuses_impossible_schedules()
    {
        // Local arrival looks later but is earlier in UTC (JED 10:00 = 07:00 UTC; LHE 11:00 = 06:00 UTC).
        TravelFlightPlanner.Plan([Seg("JED", "LHE", new DateTime(2027, 3, 1, 10, 0, 0), new DateTime(2027, 3, 1, 11, 0, 0))], Airports)
            .Error.Should().Contain("arrival is before departure");
        TravelFlightPlanner.Plan([Seg("LHE", "XXX", new DateTime(2027, 3, 1, 3, 0, 0), new DateTime(2027, 3, 1, 5, 0, 0))], Airports)
            .Error.Should().Contain("unknown airport XXX");
        TravelFlightPlanner.Plan(
            [
                Seg("LHE", "JED", new DateTime(2027, 3, 1, 3, 0, 0), new DateTime(2027, 3, 1, 5, 30, 0)),
                Seg("JED", "MED", new DateTime(2027, 3, 1, 5, 0, 0), new DateTime(2027, 3, 1, 6, 0, 0))
            ], Airports).Error.Should().Contain("departs before the previous segment arrives");
        TravelFlightPlanner.Plan([Seg("LHR", "JED", new DateTime(2027, 3, 28, 1, 30, 0), new DateTime(2027, 3, 28, 11, 0, 0))], Airports)
            .Error.Should().Contain("does not exist");
    }

    [Theory]
    [InlineData("214-2345678901", "2142345678901")]
    [InlineData("214 2345678901", "2142345678901")]
    [InlineData("2142345678901", "2142345678901")]
    [InlineData("21423456789", null)]
    [InlineData("ABC2345678901", null)]
    [InlineData("", null)]
    public void Ticket_numbers_are_13_digits(string input, string? expected) => TravelFlightPlanner.NormalizeTicket(input).Should().Be(expected);

    // ── Money ──────────────────────────────────────────────────────────────

    private static CancelTravelServiceDto Cancel(decimal sp, decimal cp, decimal rs, decimal rc) => new("Schedule change", sp, cp, rs, rc);

    [Fact]
    public void Refunds_cannot_exceed_what_was_sold_or_bought()
    {
        TravelBookingRules.ValidateCancellation(Cancel(5_000, 10_000, 90_000, 150_000), 160_000, 100_000, "PKR").Should().BeNull();
        TravelBookingRules.ValidateCancellation(Cancel(0, 20_000, 0, 150_000), 160_000, 100_000, "PKR").Should().Contain("sell amount");
        TravelBookingRules.ValidateCancellation(Cancel(20_000, 0, 90_000, 0), 160_000, 100_000, "PKR").Should().Contain("cost amount");
        TravelBookingRules.ValidateCancellation(Cancel(-1, 0, 0, 0), 160_000, 100_000, "PKR").Should().Contain("negative");
        TravelBookingRules.ValidateCancellation(Cancel(0, 0, 0, 10), null, null, null).Should().Contain("price and currency");
        TravelBookingRules.ValidateCancellation(Cancel(0, 0, 0, 0), null, null, null).Should().BeNull();
        TravelBookingRules.ValidateCancellation(Cancel(0, 0, 10, 0), 100, null, "PKR").Should().Contain("no cost amount");
    }

    [Fact]
    public void Amounts_need_a_currency()
    {
        TravelBookingRules.ValidateAmounts(100, null, null).Should().Contain("currency");
        TravelBookingRules.ValidateAmounts(100, 80, "SAR").Should().BeNull();
        TravelBookingRules.ValidateAmounts(null, null, null).Should().BeNull();
        TravelBookingRules.ValidateAmounts(-5, null, "SAR").Should().Contain("negative");
    }

    [Fact]
    public void Totals_group_by_currency_and_skip_cancelled_services()
    {
        var totals = TravelBookingRules.Totals(
            [
                new TravelFlightBookingDto { Status = "Ticketed", SellAmount = 200_000, CostAmount = 180_000, CurrencyCode = "PKR" },
                new TravelFlightBookingDto { Status = "Cancelled", SellAmount = 999, CostAmount = 999, CurrencyCode = "PKR" },
                new TravelFlightBookingDto { Status = "Requested" }
            ],
            [
                new TravelHotelBookingDto { Status = "Confirmed", SellAmount = 4_000, CostAmount = 3_200, CurrencyCode = "SAR" },
                new TravelHotelBookingDto { Status = "Confirmed", SellAmount = 50_000, CostAmount = null, CurrencyCode = "PKR" }
            ]);
        totals.Select(t => t.CurrencyCode).Should().Equal("PKR", "SAR");
        totals[0].Sell.Should().Be(250_000);
        totals[0].Cost.Should().Be(180_000);
        totals[1].Margin.Should().Be(800);
    }

    // ── Completion, gate, summary ──────────────────────────────────────────

    [Fact]
    public void A_file_completes_only_after_the_trip_and_when_nothing_is_open()
    {
        var now = new DateTime(2027, 4, 1);
        TravelBookingRules.CompleteBlocker(new TravelBookingServiceFacts(1, 1, 1, 0, now.AddDays(-5)), now).Should().Contain("not finished");
        TravelBookingRules.CompleteBlocker(new TravelBookingServiceFacts(1, 1, 0, 0, now.AddDays(5)), now).Should().Contain("not ended");
        TravelBookingRules.CompleteBlocker(new TravelBookingServiceFacts(1, 1, 0, 1, now.AddDays(-1)), now).Should().BeNull();
        TravelBookingRules.CancelBlocker(new TravelBookingServiceFacts(0, 1, 1, 2, null)).Should().NotBeNull();
        TravelBookingRules.CancelBlocker(new TravelBookingServiceFacts(0, 0, 0, 2, null)).Should().BeNull();
    }

    [Fact]
    public void Group_ticket_gate_needs_round_trip_or_both_directions()
    {
        var uncovered = TravelBookingRules.UncoveredTravelers([1, 2, 3, 4],
            [new(1, "RoundTrip"), new(2, "Outbound"), new(2, "Return"), new(3, "Outbound"), new(4, "OneWay")]);
        uncovered.Should().Equal(3, 4);
    }

    [Fact]
    public void Summary_is_zero_filled_and_sums_to_total()
    {
        var s = GetTravelBookingSummaryQueryHandler.Build(new Dictionary<string, int> { ["Open"] = 4, ["Cancelled"] = 1 }, 2);
        s.Counts.Keys.Should().BeEquivalentTo(TravelValues.BookingStatuses);
        s.Counts["Completed"].Should().Be(0);
        s.Total.Should().Be(s.Counts.Values.Sum()).And.Be(5);
        s.TicketingDueNext72Hours.Should().Be(2);
    }

    [Fact]
    public void Detail_masks_passports_without_ViewPII_and_computes_actions()
    {
        TravelBookingDto Make() => new()
        {
            Status = "Open",
            Flights = [new TravelFlightBookingDto { Status = "Confirmed", Passengers = [new TravelFlightPassengerDto { PassportNumber = "AB1234567" }] }],
            Hotels = [new TravelHotelBookingDto { Status = "Requested", CheckIn = new DateTime(2027, 3, 1), CheckOut = new DateTime(2027, 3, 5) }],
            Cancellations = [new TravelBookingCancellationDto { RefundStatus = "Pending" }]
        };

        var masked = GetTravelBookingByIdQueryHandler.Decorate(Make(), canViewPii: false, new DateTime(2027, 2, 1));
        masked.Flights[0].Passengers[0].PassportNumber.Should().NotBe("AB1234567").And.NotContain("AB1234");
        masked.Flights[0].AllowedActions.Should().Contain(["Ticket", "IssueTickets", "EditPassengers"]);
        masked.Hotels[0].AllowedActions.Should().Contain(["Confirm", "Delete"]);
        masked.Cancellations[0].NextRefundStatuses.Should().Contain("Closed");

        GetTravelBookingByIdQueryHandler.Decorate(Make(), canViewPii: true, new DateTime(2027, 2, 1))
            .Flights[0].Passengers[0].PassportNumber.Should().Be("AB1234567");
    }
}
