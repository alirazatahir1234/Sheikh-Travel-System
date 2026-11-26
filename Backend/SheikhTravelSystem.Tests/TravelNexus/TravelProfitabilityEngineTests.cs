using FluentAssertions;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

namespace SheikhTravelSystem.Tests.TravelNexus;

/// <summary>
/// One booking file: invoice 1,000 PKR (flight 600 + hotel 400) less 100 discount, 90 credit note → revenue 810.
/// Cost: a 30 SAR bill line (3 SAR supplier credit) at 10 PKR/SAR = 270, plus a 100 PKR estimate → 370. Margin 440.
/// </summary>
public class TravelProfitabilityEngineTests
{
    private static readonly DateTime Issued = new(2027, 3, 5);
    private const string Key = "Booking:1";

    private static readonly TravelContextFact Context = new("Booking", 1, "BKG-1", new DateTime(2027, 4, 1), 2, "Lahore", 7, "Sana");

    private static TravelFx Fx() => new("PKR", [("SAR", new DateTime(2027, 1, 1), 10m)]);

    private static List<TravelRevenueFact> Revenue(string currency = "PKR") =>
    [
        new(10, "TINV-1", Issued, currency, 1000, 100, 600, "Flight", "Booking", 1, "Flight"),
        new(10, "TINV-1", Issued, currency, 1000, 100, 400, "Hotel", "Booking", 1, "Hotel")
    ];

    private static readonly List<TravelInvoiceCreditFact> Credits = [new(10, 90, new DateTime(2027, 3, 20))];

    private static readonly List<TravelCostFact> Costs =
    [
        new("Booking", 1, "SAR", 30, new DateTime(2027, 3, 10), false, "TSB-1", "Hotel bill", 50),
        new("Booking", 1, "PKR", 100, new DateTime(2027, 4, 1), true, "BKG-1", "Flight (estimate)", null)
    ];

    private static readonly List<TravelBillCreditFact> BillCredits = [new(50, 30, 3, new DateTime(2027, 3, 15), "SAR")];

    private static TravelProfitabilityEngine.Result Compute(TravelFx? fx = null, string currency = "PKR")
        => TravelProfitabilityEngine.Compute([Context], Revenue(currency), Credits, Costs, BillCredits, fx ?? Fx());

    private static TravelCommissionRuleDto Rule(int id, string type, string basis, decimal rate, int? userId = null, int? subAgentId = null, string scope = "All",
        DateTime? from = null)
        => new() { Id = id, BeneficiaryType = type, UserId = userId, SubAgentId = subAgentId, Scope = scope, Basis = basis, Rate = rate, ValidFrom = from ?? new DateTime(2027, 1, 1), IsActive = true };

    private static TravelAttributedInvoiceFact Invoice(decimal balance = 500, int? agent = 7, int? subAgent = 3)
        => new(10, "TINV-1", Issued, 5, "Khan family", "PKR", balance, agent, "Sana", subAgent, "Haramain Tours");

    // ── Profit ──

    [Fact]
    public void Revenue_is_net_of_discount_and_credit_notes_spread_pro_rata()
    {
        var result = Compute();
        result.Contexts[Key].Revenue.Should().Be(810);
        result.Lines.Select(l => l.NetInInvoiceCurrency).Should().Equal(486, 324);
    }

    [Fact]
    public void Cost_is_billed_net_of_supplier_credits_plus_estimates()
    {
        var ctx = Compute().Contexts[Key];
        ctx.Cost.Should().Be(370);
        ctx.EstimatedCost.Should().Be(100);
        ctx.Lines.Single(l => l.Kind == "Cost" && !l.Estimated).Converted.Should().Be(270);
        TravelProfitabilityEngine.ToDto(ctx).Margin.Should().Be(440);
        TravelProfitabilityEngine.ToDto(ctx).HasEstimates.Should().BeTrue();
    }

    [Fact]
    public void Invoice_line_margins_add_up_to_the_context_margin()
    {
        var result = Compute();
        result.Lines.Select(l => l.Margin!.Value).Should().Equal(264, 176);
        result.Lines.Sum(l => l.Margin!.Value).Should().Be(result.Contexts[Key].Revenue - result.Contexts[Key].Cost);
    }

    [Fact]
    public void A_missing_rate_excludes_the_amount_and_is_reported()
    {
        var fx = new TravelFx("PKR", []);
        var ctx = Compute(fx).Contexts[Key];
        ctx.Cost.Should().Be(100, "only the PKR estimate converts");
        ctx.MissingRateCount.Should().Be(1);
        fx.Missing.Should().ContainSingle(m => m.Currency == "SAR");
    }

    [Fact]
    public void Revenue_without_a_rate_has_no_margin()
    {
        var result = Compute(new TravelFx("PKR", [("SAR", new DateTime(2027, 1, 1), 10m)]), currency: "USD");
        result.Lines.Should().OnlyContain(l => l.Revenue == null && l.Margin == null);
        result.Contexts[Key].Revenue.Should().Be(0);
    }

    [Fact]
    public void Groups_sum_their_contexts()
    {
        var rows = new[]
        {
            new TravelContextProfitDto { Revenue = 1000, Cost = 600, Commission = 50 },
            new TravelContextProfitDto { Revenue = 500, Cost = 550, Commission = 0 }
        };
        var group = TravelProfitabilityEngine.Sum("7", "Sana", rows);
        group.Contexts.Should().Be(2);
        group.Margin.Should().Be(300);
        group.MarginPercent.Should().Be(20);
    }

    // ── Agent commission ──

    [Fact]
    public void Staff_on_margin_and_sub_agent_on_sell_accrue_while_the_invoice_is_owed()
    {
        var profit = Compute();
        var result = TravelCommissionEngine.Compute([Invoice()], profit.Lines,
            [Rule(1, "Staff", "PercentOfMargin", 10), Rule(2, "SubAgent", "PercentOfSell", 2)], Fx());

        var staff = result.Lines.Single(l => l.BeneficiaryType == "Staff");
        staff.Amount.Should().Be(44);
        staff.CurrencyCode.Should().Be("PKR");
        staff.Status.Should().Be("Accrued");
        var sub = result.Lines.Single(l => l.BeneficiaryType == "SubAgent");
        sub.BasisAmount.Should().Be(810);
        sub.Amount.Should().Be(16.2m);
        result.CommissionByContext[Key].Should().Be(60.2m);
    }

    [Fact]
    public void Commission_becomes_payable_once_the_invoice_is_settled()
        => TravelCommissionEngine.Compute([Invoice(balance: 0)], Compute().Lines, [Rule(1, "Staff", "PercentOfMargin", 10)], Fx())
            .Lines.Should().OnlyContain(l => l.Status == "Payable");

    [Fact]
    public void The_persons_scoped_rule_beats_the_default()
    {
        var result = TravelCommissionEngine.Compute([Invoice(subAgent: null)], Compute().Lines,
            [Rule(1, "Staff", "PercentOfMargin", 10), Rule(2, "Staff", "PercentOfMargin", 5, userId: 7, scope: "Hotel"), Rule(3, "Staff", "PercentOfMargin", 50, userId: 8)],
            Fx());

        result.Lines.Should().HaveCount(2);
        result.Lines.Single(l => l.Rate == 10).Amount.Should().Be(26.4m, "the flight line falls back to the default rule");
        result.Lines.Single(l => l.Rate == 5).Amount.Should().Be(8.8m, "the hotel line uses the agent's own hotel rule");
    }

    [Fact]
    public void A_rule_that_starts_after_the_issue_date_does_not_apply()
        => TravelCommissionEngine.Compute([Invoice(subAgent: null)], Compute().Lines,
                [Rule(1, "Staff", "PercentOfMargin", 10, from: new DateTime(2027, 6, 1))], Fx())
            .Lines.Should().BeEmpty();

    [Fact]
    public void Margin_commission_is_never_negative()
    {
        var lossCosts = new List<TravelCostFact> { new("Booking", 1, "PKR", 5000, Issued, false, "TSB-9", "Hotel", 51) };
        var profit = TravelProfitabilityEngine.Compute([Context], Revenue(), Credits, lossCosts, [], Fx());
        TravelCommissionEngine.Compute([Invoice(subAgent: null)], profit.Lines, [Rule(1, "Staff", "PercentOfMargin", 10)], Fx())
            .Lines.Single().Amount.Should().Be(0);
    }

    [Fact]
    public void Margin_commission_with_a_missing_rate_is_flagged_and_excluded_from_balances()
    {
        var fx = new TravelFx("PKR", []);
        var profit = TravelProfitabilityEngine.Compute([Context], Revenue("SAR"), [], [], [], fx);
        var result = TravelCommissionEngine.Compute([Invoice(subAgent: null) with { CurrencyCode = "SAR" }], profit.Lines, [Rule(1, "Staff", "PercentOfMargin", 10)], fx);

        result.Lines.Single().MissingRate.Should().BeTrue();
        TravelCommissionEngine.Balances(result.Lines, []).Should().BeEmpty();
    }

    [Fact]
    public void Balance_goes_negative_when_paid_out_more_than_is_now_payable()
    {
        var lines = TravelCommissionEngine.Compute([Invoice(balance: 0, subAgent: null)], Compute().Lines, [Rule(1, "Staff", "PercentOfMargin", 10)], Fx()).Lines;
        var balance = TravelCommissionEngine.Balances(lines,
            [new TravelCommissionPayoutDto { BeneficiaryType = "Staff", BeneficiaryId = 7, BeneficiaryName = "Sana", Amount = 50, CurrencyCode = "PKR" }]).Single();

        balance.Payable.Should().Be(44);
        balance.Paid.Should().Be(50);
        balance.Balance.Should().Be(-6);
    }

    [Fact]
    public void Accrued_commission_is_not_payable_yet()
    {
        var lines = TravelCommissionEngine.Compute([Invoice()], Compute().Lines, [Rule(1, "Staff", "PercentOfMargin", 10)], Fx()).Lines;
        var balance = TravelCommissionEngine.Balances(lines, []).Single();
        balance.Accrued.Should().Be(44);
        balance.Payable.Should().Be(0);
        balance.Balance.Should().Be(0);
    }
}
