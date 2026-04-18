using FluentAssertions;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Payments;

public class PaymentStatusLogicTests
{
    // Mirrors CreatePaymentCommandHandler business logic
    private static PaymentStatus DeterminePaymentStatus(decimal totalAmount, decimal alreadyPaid, decimal newPayment)
    {
        return (alreadyPaid + newPayment) >= totalAmount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;
    }

    private static bool ExceedsRemainingBalance(decimal totalAmount, decimal alreadyPaid, decimal newPayment)
    {
        var remaining = totalAmount - alreadyPaid;
        return newPayment > remaining;
    }

    [Fact]
    public void FullPayment_ShouldResultInPaidStatus()
    {
        var status = DeterminePaymentStatus(totalAmount: 10000m, alreadyPaid: 0m, newPayment: 10000m);
        status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void PartialPayment_ShouldResultInPartiallyPaidStatus()
    {
        var status = DeterminePaymentStatus(totalAmount: 10000m, alreadyPaid: 0m, newPayment: 5000m);
        status.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public void FinalPartialPayment_ShouldResultInPaidStatus()
    {
        // First partial = 4000, second payment = 6000 → total = 10000 = Paid
        var status = DeterminePaymentStatus(totalAmount: 10000m, alreadyPaid: 4000m, newPayment: 6000m);
        status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void SecondPartialPayment_ShouldStillBePartiallyPaid()
    {
        var status = DeterminePaymentStatus(totalAmount: 10000m, alreadyPaid: 3000m, newPayment: 3000m);
        status.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public void PaymentExactlyEqualToTotal_ShouldBePaid()
    {
        var status = DeterminePaymentStatus(totalAmount: 7500m, alreadyPaid: 2500m, newPayment: 5000m);
        status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void PaymentOver100Percent_DoesNotExceedCheck_ShouldBePaid()
    {
        // Handler would block this before reaching status calc, but logic still returns Paid
        var status = DeterminePaymentStatus(totalAmount: 5000m, alreadyPaid: 0m, newPayment: 6000m);
        status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void ExceedsRemainingBalance_ShouldReturnTrue()
    {
        ExceedsRemainingBalance(10000m, 0m, 12000m).Should().BeTrue();
    }

    [Fact]
    public void ExactlyRemainingBalance_ShouldNotExceed()
    {
        ExceedsRemainingBalance(10000m, 4000m, 6000m).Should().BeFalse();
    }

    [Fact]
    public void BelowRemainingBalance_ShouldNotExceed()
    {
        ExceedsRemainingBalance(10000m, 3000m, 5000m).Should().BeFalse();
    }

    [Fact]
    public void OneMoreThanRemaining_ShouldExceed()
    {
        ExceedsRemainingBalance(10000m, 9999m, 2m).Should().BeTrue();
    }

    [Fact]
    public void CreatePaymentDto_ShouldHoldAllFields()
    {
        var dto = new CreatePaymentDto(
            BookingId: 5,
            Amount: 3000m,
            PaymentMethod: "Cash",
            TransactionReference: "TXN123",
            Notes: "Half payment");

        dto.BookingId.Should().Be(5);
        dto.Amount.Should().Be(3000m);
        dto.PaymentMethod.Should().Be("Cash");
        dto.TransactionReference.Should().Be("TXN123");
        dto.Notes.Should().Be("Half payment");
    }

    [Fact]
    public void PaymentDto_ShouldHoldAllFields()
    {
        var paymentDate = DateTime.UtcNow;
        var dto = new PaymentDto(
            Id: 1, BookingId: 5, Amount: 5000m,
            PaymentMethod: "Bank Transfer", Status: PaymentStatus.Paid,
            PaymentDate: paymentDate, TransactionReference: "TXN001",
            Notes: null, CreatedAt: paymentDate);

        dto.Id.Should().Be(1);
        dto.BookingId.Should().Be(5);
        dto.Amount.Should().Be(5000m);
        dto.Status.Should().Be(PaymentStatus.Paid);
        dto.PaymentMethod.Should().Be("Bank Transfer");
    }

    [Fact]
    public void PaymentReportDto_ShouldAggregateCorrectly()
    {
        var report = new PaymentReportDto(
            TotalReceived: 50000m,
            TotalPending: 10000m,
            TotalTransactions: 15,
            RecentPayments: new List<PaymentDto>());

        report.TotalReceived.Should().Be(50000m);
        report.TotalPending.Should().Be(10000m);
        report.TotalTransactions.Should().Be(15);
        report.RecentPayments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.PartiallyPaid)]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Refunded)]
    public void PaymentStatus_AllEnumValues_ShouldExist(PaymentStatus status)
    {
        Enum.IsDefined(typeof(PaymentStatus), status).Should().BeTrue();
    }

    [Fact]
    public void CreatePaymentCommand_ShouldWrapDto()
    {
        var dto = new CreatePaymentDto(1, 500m, "Cash", null, null);
        var cmd = new CreatePaymentCommand(dto);
        cmd.Payment.Should().Be(dto);
    }
}
