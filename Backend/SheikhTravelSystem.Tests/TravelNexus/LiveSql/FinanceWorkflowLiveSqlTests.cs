using FluentAssertions;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports;
using SheikhTravelSystem.Application.Features.TravelNexus.Reports.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Queries;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;
using F = SheikhTravelSystem.Application.Features.TravelNexus.Bookings.TravelFlightStateMachine.Actions;

namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

/// <summary>
/// Brief workflow 3 against a real database: supplier → booking file → confirmed flight → issued invoice → receipt
/// (idempotent) → posted supplier bill → supplier payment → commission payable → payout capped at the payable balance;
/// statement and profit report agree; company B sees no money at all.
/// </summary>
[Collection(TravelNexusLiveSqlCollection.Name)]
public class FinanceWorkflowLiveSqlTests(TravelNexusLiveDbFixture fx)
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;
    private const decimal Sell = 150000m;
    private const decimal Cost = 120000m;
    private const decimal CommissionRate = 10m;

    [TravelLiveSqlFact]
    public async Task Booking_invoice_receipt_bill_and_commission_payout_end_to_end_and_invisible_to_company_B()
    {
        using var a = fx.Open(fx.A);
        var depart = Today.AddDays(30).AddHours(9);

        // ── Supplier, customer, traveler, booking file ──────────────────────
        var supplierId = await a.Ok(new SaveTravelSupplierCommand(null,
            new SaveTravelSupplierDto("PIA (E2E)", "Airline", "PK", null, null, null, "PKR", true, null)));
        var customerId = await a.Ok(new CreateTravelCustomerCommand(new CreateTravelCustomerDto(
            null, "Individual", "Sara Malik", "+92 321 0000001", null, null, "Karachi", null, null, null, null, fx.A.UserId, fx.A.BranchId, null, null)));
        var travelerId = await a.Ok(new CreateTravelTravelerCommand(customerId, new SaveTravelTravelerDto(
            null, "Sara", "Malik", "Female", new DateTime(1990, 3, 3), null, "PK", "FIN000001", "PK",
            new DateTime(2022, 1, 1), new DateTime(2032, 1, 1), null, null, null, null, null, null)));
        var bookingId = await a.Ok(new CreateTravelBookingCommand(new CreateTravelBookingDto(
            customerId, null, "KHI–JED return (E2E)", null, null, fx.A.UserId, fx.A.BranchId, null)));

        // ── Flight with sell and cost, confirmed with its PNR ───────────────
        var flightId = await a.Ok(new SaveTravelFlightCommand(bookingId, null, new SaveTravelFlightDto(
            supplierId, "RoundTrip", "E2EPNR1", null, Sell, Cost, "PKR", null,
            [
                new SaveTravelFlightSegmentDto("PK", "741", "KHI", "JED", depart, depart.AddHours(5), "Economy", "Y", "30kg"),
                new SaveTravelFlightSegmentDto("PK", "742", "JED", "KHI", depart.AddDays(10), depart.AddDays(10).AddHours(5), "Economy", "Y", "30kg"),
            ],
            [new SaveTravelFlightPassengerDto(travelerId, null, "Adult")])));
        await a.Ok(new TransitionTravelFlightCommand(bookingId, flightId, new TransitionTravelFlightDto(F.Confirm, "E2EPNR1", null, null)));
        (await a.Ok(new GetTravelBookingByIdQuery(bookingId))).Flights.Should().ContainSingle(f => f.Id == flightId && f.Status == "Confirmed");

        // ── Commission rule: the agent earns 10 % of sell on everything ─────
        await a.Ok(new SaveTravelCommissionRuleCommand(null,
            new SaveTravelCommissionRuleDto("Staff", fx.A.UserId, null, "All", "PercentOfSell", CommissionRate, Today.AddDays(-30), null, true)));

        // ── Invoice the flight, issue it, collect it (idempotent receipt) ────
        var invoiceId = await a.Ok(new CreateTravelInvoiceCommand(new CreateTravelInvoiceDto(customerId, "PKR", Today, Today.AddDays(7), 0m, null,
            [new SaveTravelInvoiceLineDto(string.Empty, 1, Sell, "Flight", flightId)], [], fx.A.UserId, null)));
        await a.Ok(new IssueTravelInvoiceCommand(invoiceId, new IssueTravelInvoiceDto(Today, Today.AddDays(7))));

        var secondInvoice = await a.Send(new CreateTravelInvoiceCommand(new CreateTravelInvoiceDto(customerId, "PKR", Today, Today.AddDays(7), 0m, null,
            [new SaveTravelInvoiceLineDto(string.Empty, 1, Sell, "Flight", flightId)], [], fx.A.UserId, null)));
        secondInvoice.Success.Should().BeFalse("a sold item is invoiced once");

        var receiptKey = Guid.NewGuid();
        var receipt = new RecordTravelPaymentCommand(new RecordTravelPaymentDto(receiptKey, customerId, Today, "BankTransfer", "TRX-E2E-1", Sell, "PKR",
            Cleared: true, null, [new TravelAllocationInputDto(invoiceId, Sell, null)]));
        var paymentId = await a.Ok(receipt);
        var replay = await a.Send(receipt);
        replay.Success.Should().BeTrue();
        replay.Data.Should().Be(paymentId, "the same idempotency key returns the same receipt");
        replay.Message.Should().Contain("already");

        var invoice = await a.Ok(new GetTravelInvoiceByIdQuery(invoiceId));
        invoice.Status.Should().Be("Issued");
        invoice.Total.Should().Be(Sell);
        invoice.Balance.Should().Be(0m);
        invoice.PaymentStatus.Should().Be("Paid");
        invoice.AgentUserId.Should().Be(fx.A.UserId);

        // ── Supplier bill for the same flight, posted and paid ──────────────
        var billId = await a.Ok(new CreateTravelSupplierBillCommand(new CreateTravelSupplierBillDto(supplierId, "PKR", "PIA-E2E-1", Today, Today.AddDays(14), null,
            [new SaveTravelSupplierBillLineDto(string.Empty, "Flight", flightId, null, null, Cost, 0m)])));
        await a.Ok(new PostTravelSupplierBillCommand(billId, new PostTravelSupplierBillDto(Today, Today.AddDays(14))));
        await a.Ok(new RecordTravelSupplierPaymentCommand(new RecordTravelSupplierPaymentDto(Guid.NewGuid(), supplierId, Today, "BankTransfer", "SUP-E2E-1",
            Cost, "PKR", Cleared: true, null, [new TravelAllocationInputDto(billId, Cost, null)])));
        (await a.Ok(new GetTravelSupplierBalancesQuery(supplierId, OpenOnly: true))).Should().BeEmpty("the bill is settled");

        // ── Commission: payable once the invoice is paid; payouts never exceed it ──
        var payable = Math.Round(Sell * CommissionRate / 100m, 2);
        var balances = await a.Ok(new GetTravelCommissionBalancesQuery("Staff", fx.A.UserId));
        var balance = balances.Should().ContainSingle(b => b.CurrencyCode == "PKR").Subject;
        balance.Payable.Should().Be(payable);
        balance.Paid.Should().Be(0m);

        var tooMuch = await a.Send(new RecordTravelCommissionPayoutCommand(new RecordTravelCommissionPayoutDto(
            Guid.NewGuid(), "Staff", fx.A.UserId, Today, "Cash", null, payable + 1m, "PKR", null)));
        tooMuch.Success.Should().BeFalse();
        tooMuch.Message.Should().Contain("payable");

        var payoutKey = Guid.NewGuid();
        var payout = new RecordTravelCommissionPayoutCommand(new RecordTravelCommissionPayoutDto(
            payoutKey, "Staff", fx.A.UserId, Today, "Cash", null, payable, "PKR", null));
        var payoutId = await a.Ok(payout);
        var payoutReplay = await a.Send(payout);
        payoutReplay.Success.Should().BeTrue("a retry after the payable is used up returns the original payout");
        payoutReplay.Data.Should().Be(payoutId);
        var nothingLeft = await a.Send(new RecordTravelCommissionPayoutCommand(new RecordTravelCommissionPayoutDto(
            Guid.NewGuid(), "Staff", fx.A.UserId, Today, "Cash", null, 1m, "PKR", null)));
        nothingLeft.Success.Should().BeFalse();
        (await a.Ok(new GetTravelCommissionBalancesQuery("Staff", fx.A.UserId))).Single(b => b.CurrencyCode == "PKR").Balance.Should().Be(0m);

        // ── Statement and profit report agree with the ledger ───────────────
        var statement = await a.Ok(new GetTravelCustomerStatementQuery(customerId, "PKR", null, null));
        statement.Entries.Should().Contain(e => e.Debit == Sell).And.Contain(e => e.Credit == Sell);
        statement.Entries.Last().Balance.Should().Be(0m);

        var profit = await a.Ok(new GetTravelReportQuery(TravelReportGroup.Profit, "profit-loss", Today.AddDays(-1), Today.AddDays(60), null, null, null, null));
        profit.Rows.Should().NotBeEmpty("the flight travels inside the window");
        profit.Summary.Should().ContainKey("reportingCurrencyCode").WhoseValue.Should().Be("PKR");

        // ── Company B ───────────────────────────────────────────────────────
        using var b = fx.Open(fx.B);
        await b.Invoking(s => s.Send(new GetTravelInvoiceByIdQuery(invoiceId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new GetTravelBookingByIdQuery(bookingId))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new GetTravelCustomerStatementQuery(customerId, "PKR", null, null))).Should().ThrowAsync<NotFoundException>();
        await b.Invoking(s => s.Send(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(Guid.NewGuid(), customerId, Today, "Cash", null, 1m, "PKR", true, null, []))))
            .Should().ThrowAsync<NotFoundException>();
        (await b.Ok(new GetTravelCustomerBalancesQuery(null, OpenOnly: false))).Should().BeEmpty();
        (await b.Ok(new GetTravelCommissionBalancesQuery(null, null))).Should().BeEmpty();
        (await b.Ok(new GetTravelReportQuery(TravelReportGroup.Profit, "profit-loss", Today.AddDays(-1), Today.AddDays(60), null, null, null, null))).Rows.Should().BeEmpty();
    }

    [TravelLiveSqlFact]
    public async Task Two_concurrent_payouts_at_the_cap_let_exactly_one_through()
    {
        using var a = fx.Open(fx.A);
        const decimal sell = 100000m;
        const decimal rate = 5m;
        var payable = sell * rate / 100m;
        var depart = Today.AddDays(40).AddHours(7);

        // A sub-agent referred the customer; their 5 % of sell becomes payable once the invoice is settled. No staff agent is
        // assigned, so this invoice never adds to the staff balance the other finance test asserts (both share company A).
        var subAgentId = await a.Ok(new SaveTravelSubAgentCommand(null, new SaveTravelSubAgentDto("Haramain Tours (E2E)", null, null, null, true)));
        var customerId = await a.Ok(new CreateTravelCustomerCommand(new CreateTravelCustomerDto(
            null, "Individual", "Zain Raza", "+92 333 0000002", null, null, "Karachi", null, null, null, null, null, fx.A.BranchId, null, null, subAgentId)));
        var travelerId = await a.Ok(new CreateTravelTravelerCommand(customerId, new SaveTravelTravelerDto(
            null, "Zain", "Raza", "Male", new DateTime(1992, 7, 7), null, "PK", "FIN000002", "PK",
            new DateTime(2022, 1, 1), new DateTime(2032, 1, 1), null, null, null, null, null, null)));
        var bookingId = await a.Ok(new CreateTravelBookingCommand(new CreateTravelBookingDto(customerId, null, "KHI–JED one way (E2E cap)", null, null, null, fx.A.BranchId, null)));
        var flightId = await a.Ok(new SaveTravelFlightCommand(bookingId, null, new SaveTravelFlightDto(
            null, "OneWay", null, null, sell, null, "PKR", null,
            [new SaveTravelFlightSegmentDto("PK", "743", "KHI", "JED", depart, depart.AddHours(5), "Economy", null, null)],
            [new SaveTravelFlightPassengerDto(travelerId, null, "Adult")])));
        await a.Ok(new SaveTravelCommissionRuleCommand(null,
            new SaveTravelCommissionRuleDto("SubAgent", null, subAgentId, "All", "PercentOfSell", rate, Today.AddDays(-30), null, true)));
        var invoiceId = await a.Ok(new CreateTravelInvoiceCommand(new CreateTravelInvoiceDto(customerId, "PKR", Today, Today.AddDays(7), 0m, null,
            [new SaveTravelInvoiceLineDto(string.Empty, 1, sell, "Flight", flightId)], [], null, subAgentId)));
        await a.Ok(new IssueTravelInvoiceCommand(invoiceId, new IssueTravelInvoiceDto(Today, Today.AddDays(7))));
        await a.Ok(new RecordTravelPaymentCommand(new RecordTravelPaymentDto(Guid.NewGuid(), customerId, Today, "Cash", null, sell, "PKR", true, null,
            [new TravelAllocationInputDto(invoiceId, sell, null)])));
        (await a.Ok(new GetTravelCommissionBalancesQuery("SubAgent", subAgentId))).Single(x => x.CurrencyCode == "PKR").Payable.Should().Be(payable);

        // Two requests race for the whole payable: both pass the handler's read, only one survives the repository's lock.
        using var s1 = fx.Open(fx.A);
        using var s2 = fx.Open(fx.A);
        RecordTravelCommissionPayoutCommand Payout() => new(new RecordTravelCommissionPayoutDto(Guid.NewGuid(), "SubAgent", subAgentId, Today, "Cash", null, payable, "PKR", null));
        var results = await Task.WhenAll(s1.Send(Payout()), s2.Send(Payout()));

        results.Count(r => r.Success).Should().Be(1, string.Join(" | ", results.Select(r => r.Message)));
        results.Single(r => !r.Success).Message.Should().MatchRegex("payable|already");
        var paid = (await a.Ok(new GetTravelCommissionPayoutsQuery("SubAgent", subAgentId))).Sum(p => p.Amount);
        paid.Should().Be(payable, "never more than the payable commission leaves the company");
        (await a.Ok(new GetTravelCommissionBalancesQuery("SubAgent", subAgentId))).Single(x => x.CurrencyCode == "PKR").Balance.Should().Be(0m);
    }
}
