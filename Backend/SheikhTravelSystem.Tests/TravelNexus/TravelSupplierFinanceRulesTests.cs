using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelSupplierFinanceRulesTests
{
    private static readonly DateTime BillDate = new(2027, 3, 10);

    private static TravelBillableCostDto Hotel(int id, decimal amount, string currency = "SAR", int units = 20)
        => new() { SourceType = "Hotel", SourceId = id, SupplierId = 4, Description = $"Hotel {id}", Amount = amount, CurrencyCode = currency, Units = units };

    private static TravelSupplierCommissionRuleDto Rule(int id, string service, string basis, decimal rate, DateTime from, DateTime? to = null,
        string? currency = null, bool active = true)
        => new() { Id = id, SupplierId = 4, ServiceType = service, Basis = basis, Rate = rate, CurrencyCode = currency, ValidFrom = from, ValidTo = to, IsActive = active };

    // ── Bill state machine ──

    [Fact]
    public void Draft_bill_can_be_edited_posted_and_deleted()
        => TravelSupplierBillStateMachine.AllowedActions(new TravelSupplierBillState { Status = "Draft", Total = 100 })
            .Should().BeEquivalentTo(["Edit", "Post", "Delete"]);

    [Fact]
    public void Posted_bill_without_money_movements_can_be_paid_credited_and_voided()
        => TravelSupplierBillStateMachine.AllowedActions(new TravelSupplierBillState { Status = "Posted", Total = 7600 })
            .Should().BeEquivalentTo(["RecordPayment", "Credit", "Void"]);

    [Fact]
    public void Posted_bill_with_a_payment_cannot_be_voided()
        => TravelSupplierBillStateMachine.AllowedActions(new TravelSupplierBillState { Status = "Posted", Total = 7600, Paid = 4000, AllocationCount = 1 })
            .Should().NotContain("Void").And.Contain("RecordPayment");

    [Fact]
    public void Overpaid_bill_offers_a_refund()
        => TravelSupplierBillStateMachine.AllowedActions(new TravelSupplierBillState { Status = "Posted", Total = 1000, Paid = 1000, Credited = 200, AllocationCount = 1, CreditCount = 1 })
            .Should().Contain("Refund").And.NotContain("RecordPayment");

    [Fact]
    public void Supplier_balance_is_net_less_credits_less_net_payments()
        => new TravelSupplierBillState { Total = 7600, Paid = 5000, Refunded = 400, Credited = 1000 }.Balance.Should().Be(2000);

    // ── Building a bill ──

    [Fact]
    public void Build_sums_gross_commission_and_net()
    {
        var (plan, error) = TravelSupplierFinanceRules.Build(
            [new SaveTravelSupplierBillLineDto("", "Hotel", 1, null, null, 8000, 400), new SaveTravelSupplierBillLineDto("Ziyarat coach", null, null, "Group", 3, 800, 0)],
            [Hotel(1, 8000)], "SAR");

        error.Should().BeNull();
        plan!.Gross.Should().Be(8800);
        plan.Commission.Should().Be(400);
        plan.Net.Should().Be(8400);
        plan.Lines[0].Description.Should().Be("Hotel 1", "a sourced line without a description takes the service's");
        plan.Lines[1].ContextType.Should().Be("Group");
        plan.Lines[1].NetAmount.Should().Be(800);
    }

    [Fact]
    public void Build_refuses_the_same_service_twice()
        => TravelSupplierFinanceRules.Build(
                [new SaveTravelSupplierBillLineDto("a", "Hotel", 1, null, null, 100, 0), new SaveTravelSupplierBillLineDto("b", "Hotel", 1, null, null, 100, 0)],
                [Hotel(1, 100)], "SAR").Error
            .Should().Contain("listed twice");

    [Fact]
    public void Build_refuses_a_service_that_is_not_billable()
        => TravelSupplierFinanceRules.Build([new SaveTravelSupplierBillLineDto("x", "Hotel", 99, null, null, 100, 0)], [Hotel(1, 100)], "SAR").Error
            .Should().Contain("already billed");

    [Fact]
    public void Build_refuses_a_service_in_another_currency()
        => TravelSupplierFinanceRules.Build([new SaveTravelSupplierBillLineDto("x", "Hotel", 1, null, null, 100, 0)], [Hotel(1, 100, "USD")], "SAR").Error
            .Should().Contain("USD");

    [Fact]
    public void Build_refuses_commission_above_the_line_amount()
        => TravelSupplierFinanceRules.Build([new SaveTravelSupplierBillLineDto("Coach", null, null, null, null, 100, 150)], [], "SAR").Error
            .Should().Contain("commission cannot exceed");

    [Fact]
    public void Build_refuses_a_manual_line_with_an_unknown_context()
        => TravelSupplierFinanceRules.Build([new SaveTravelSupplierBillLineDto("Coach", null, null, "Trip", 4, 100, 0)], [], "SAR").Error
            .Should().Contain("unknown booking");

    [Fact]
    public void Build_needs_a_line()
        => TravelSupplierFinanceRules.Build([], [], "SAR").Error.Should().NotBeNull();

    // ── Supplier commission suggestion ──

    [Fact]
    public void Percent_of_cost_commission()
        => TravelSupplierFinanceRules.SuggestCommission([Rule(1, "Hotel", "PercentOfCost", 5, new DateTime(2027, 1, 1))], "Hotel", 8000, 20, "SAR", BillDate)
            .Should().Be(400);

    [Fact]
    public void Fixed_commission_per_room_night_in_the_bill_currency()
        => TravelSupplierFinanceRules.SuggestCommission([Rule(1, "Hotel", "FixedPerUnit", 15, new DateTime(2027, 1, 1), currency: "SAR")], "Hotel", 8000, 20, "SAR", BillDate)
            .Should().Be(300);

    [Fact]
    public void Fixed_commission_in_another_currency_is_not_converted()
        => TravelSupplierFinanceRules.SuggestCommission([Rule(1, "Flight", "FixedPerUnit", 10, new DateTime(2027, 1, 1), currency: "USD")], "Flight", 900, 3, "SAR", BillDate)
            .Should().Be(0);

    [Fact]
    public void Commission_is_capped_at_the_amount()
        => TravelSupplierFinanceRules.SuggestCommission([Rule(1, "Flight", "FixedPerUnit", 500, new DateTime(2027, 1, 1), currency: "SAR")], "Flight", 600, 3, "SAR", BillDate)
            .Should().Be(600);

    [Fact]
    public void The_latest_rule_in_force_on_the_bill_date_applies()
    {
        var rules = new[]
        {
            Rule(1, "Hotel", "PercentOfCost", 5, new DateTime(2027, 1, 1)),
            Rule(2, "Hotel", "PercentOfCost", 7, new DateTime(2027, 3, 1)),
            Rule(3, "Hotel", "PercentOfCost", 9, new DateTime(2027, 4, 1)),
            Rule(4, "Hotel", "PercentOfCost", 20, new DateTime(2027, 3, 5), active: false),
            Rule(5, "Flight", "PercentOfCost", 30, new DateTime(2027, 3, 5))
        };
        TravelSupplierFinanceRules.SuggestCommission(rules, "Hotel", 1000, 1, "SAR", BillDate).Should().Be(70);
    }

    [Fact]
    public void An_expired_rule_does_not_apply()
        => TravelSupplierFinanceRules.SuggestCommission([Rule(1, "Hotel", "PercentOfCost", 5, new DateTime(2026, 1, 1), new DateTime(2027, 3, 9))], "Hotel", 1000, 1, "SAR", BillDate)
            .Should().Be(0);

    // ── Rate cards ──

    private static TravelSupplierRateDto Card(int id, string room, DateTime from, DateTime to, decimal amount, string currency = "SAR")
        => new() { Id = id, SupplierId = 4, RateType = "HotelRoomNight", HotelId = 12, RoomType = room, ValidFrom = from, ValidTo = to, Amount = amount, CurrencyCode = currency, IsActive = true };

    [Fact]
    public void Rate_card_quote_is_nights_times_rooms_times_rate()
    {
        var quote = TravelRateQuote.Quote([Card(1, "Double", new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), 400)], 12,
            new DateTime(2027, 3, 1), new DateTime(2027, 3, 11), [new TravelRateQuoteRoomDto("Double", 2)]);

        quote.Nights.Should().Be(10);
        quote.Total.Should().Be(8000);
        quote.CurrencyCode.Should().Be("SAR");
        quote.Complete.Should().BeTrue();
    }

    [Fact]
    public void Nights_without_a_rate_are_reported_not_guessed()
    {
        var quote = TravelRateQuote.Quote([Card(1, "Double", new DateTime(2027, 1, 1), new DateTime(2027, 3, 8), 400)], 12,
            new DateTime(2027, 3, 7), new DateTime(2027, 3, 10), [new TravelRateQuoteRoomDto("Double", 1), new TravelRateQuoteRoomDto("Quad", 1)]);

        quote.Total.Should().Be(800, "only 7 and 8 March have a Double rate");
        quote.Missing.Should().HaveCount(4, "the Quad has no card at all and the Double has none on 9 March");
        quote.Complete.Should().BeFalse();
    }

    [Fact]
    public void A_newer_overlapping_card_wins()
    {
        var quote = TravelRateQuote.Quote(
            [Card(1, "Double", new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), 400), Card(2, "Double", new DateTime(2027, 3, 1), new DateTime(2027, 3, 31), 550)], 12,
            new DateTime(2027, 2, 28), new DateTime(2027, 3, 2), [new TravelRateQuoteRoomDto("Double", 1)]);
        quote.Total.Should().Be(950);
    }

    [Fact]
    public void Mixed_currency_cards_give_no_total()
    {
        var quote = TravelRateQuote.Quote(
            [Card(1, "Double", new DateTime(2027, 3, 1), new DateTime(2027, 3, 1), 400), Card(2, "Double", new DateTime(2027, 3, 2), new DateTime(2027, 3, 2), 110, "USD")], 12,
            new DateTime(2027, 3, 1), new DateTime(2027, 3, 3), [new TravelRateQuoteRoomDto("Double", 1)]);
        quote.MixedCurrencies.Should().BeTrue();
        quote.CurrencyCode.Should().BeNull();
    }

    // ── FX ──

    [Fact]
    public void Fx_uses_the_latest_rate_in_force_on_the_date()
    {
        var fx = new TravelFx("PKR", [("SAR", new DateTime(2027, 1, 1), 74m), ("SAR", new DateTime(2027, 3, 1), 74.2m)]);
        fx.Convert(100, "SAR", new DateTime(2027, 2, 28)).Should().Be(7400);
        fx.Convert(100, "SAR", new DateTime(2027, 3, 1)).Should().Be(7420);
        fx.Convert(100, "PKR", new DateTime(2020, 1, 1)).Should().Be(100);
        fx.Missing.Should().BeEmpty();
    }

    [Fact]
    public void Fx_never_guesses_a_missing_rate()
    {
        var fx = new TravelFx("PKR", [("SAR", new DateTime(2027, 3, 1), 74.2m)]);
        fx.Convert(100, "SAR", new DateTime(2027, 2, 1)).Should().BeNull("the first SAR rate starts later");
        fx.Convert(50, "USD", new DateTime(2027, 4, 1)).Should().BeNull();
        fx.Convert(10, "USD", new DateTime(2027, 3, 1)).Should().BeNull();
        fx.Missing.Should().BeEquivalentTo([("SAR", new DateTime(2027, 2, 1), 1), ("USD", new DateTime(2027, 3, 1), 2)]);
    }
}
