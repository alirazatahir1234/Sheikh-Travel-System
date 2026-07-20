using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record DriverTripPaymentSummaryDto(
    int TripId,
    int BookingId,
    string BookingNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    bool PaymentRequired,
    string PaymentStatus);

public record GetDriverTripPaymentSummaryQuery(int Id) : IRequest<ApiResponse<DriverTripPaymentSummaryDto>>;

public record DriverCollectPaymentCommand(
    int Id,
    decimal AmountReceived,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Notes) : IRequest<ApiResponse<int>>;

public class DriverCollectPaymentCommandValidator : AbstractValidator<DriverCollectPaymentCommand>
{
    public DriverCollectPaymentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AmountReceived).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ReferenceNumber)
            .NotEmpty()
            .When(x => !string.Equals(x.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Reference number is required for non-cash payments.");
    }
}

public class GetDriverTripPaymentSummaryQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverTripPaymentSummaryQuery, ApiResponse<DriverTripPaymentSummaryDto>>
{
    public async Task<ApiResponse<DriverTripPaymentSummaryDto>> Handle(GetDriverTripPaymentSummaryQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverTripPaymentSummaryDto>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await ResolveDriverBookingAsync(connection, request.Id, driverId.Value, tenantId, cancellationToken);
        if (row is null)
            return ApiResponse<DriverTripPaymentSummaryDto>.FailResponse("Trip not found or not assigned to you.");

        var paidAmount = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(Amount), 0)
              FROM Payments
              WHERE BookingId = @BookingId AND IsDeleted = 0 AND Status IN (@Paid, @Partial)",
            new { row.BookingId, Paid = (int)PaymentStatus.Paid, Partial = (int)PaymentStatus.PartiallyPaid },
            cancellationToken: cancellationToken));

        var balance = Math.Max(0, row.TotalAmount - paidAmount);
        var status = balance <= 0
            ? "Paid"
            : (paidAmount > 0 ? "PartiallyPaid" : "Pending");

        return ApiResponse<DriverTripPaymentSummaryDto>.SuccessResponse(new DriverTripPaymentSummaryDto(
            request.Id,
            row.BookingId,
            row.BookingNumber,
            row.TotalAmount,
            paidAmount,
            balance,
            balance > 0,
            status));
    }

    internal static async Task<DriverBookingRef?> ResolveDriverBookingAsync(
        System.Data.IDbConnection connection,
        int id,
        int driverId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var fromTrip = await connection.QuerySingleOrDefaultAsync<DriverBookingRef>(new CommandDefinition(
            @"SELECT TOP 1 b.Id AS BookingId, b.BookingNumber, b.TotalAmount
              FROM Trips t
              INNER JOIN Bookings b ON b.Id = t.BookingId
              WHERE t.TenantId = @TenantId AND t.DriverId = @DriverId AND t.IsDeleted = 0
                AND b.TenantId = @TenantId AND b.IsDeleted = 0
                AND (t.Id = @Id OR t.BookingId = @Id)",
            new { Id = id, DriverId = driverId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        if (fromTrip is not null) return fromTrip;

        return await connection.QuerySingleOrDefaultAsync<DriverBookingRef>(new CommandDefinition(
            @"SELECT TOP 1 b.Id AS BookingId, b.BookingNumber, b.TotalAmount
              FROM Bookings b
              WHERE b.Id = @Id AND b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0",
            new { Id = id, DriverId = driverId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    internal sealed class DriverBookingRef
    {
        public int BookingId { get; init; }
        public string BookingNumber { get; init; } = "";
        public decimal TotalAmount { get; init; }
    }
}

public class DriverCollectPaymentCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ISender sender)
    : IRequestHandler<DriverCollectPaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(DriverCollectPaymentCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<int>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await GetDriverTripPaymentSummaryQueryHandler.ResolveDriverBookingAsync(
            connection, request.Id, driverId.Value, tenantId, cancellationToken);
        if (row is null)
            return ApiResponse<int>.FailResponse("Trip not found or not assigned to you.");

        var paidAmount = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(Amount), 0)
              FROM Payments
              WHERE BookingId = @BookingId AND IsDeleted = 0 AND Status IN (@Paid, @Partial)",
            new { row.BookingId, Paid = (int)PaymentStatus.Paid, Partial = (int)PaymentStatus.PartiallyPaid },
            cancellationToken: cancellationToken));
        var balance = Math.Max(0, row.TotalAmount - paidAmount);
        if (balance <= 0)
            return ApiResponse<int>.FailResponse("Payment already settled for this trip.");
        if (request.AmountReceived > balance)
            return ApiResponse<int>.FailResponse($"Amount exceeds balance due ({balance:N2}).");

        var create = await sender.Send(new CreatePaymentCommand(new CreatePaymentDto(
            row.BookingId,
            request.AmountReceived,
            request.PaymentMethod,
            request.ReferenceNumber,
            $"CollectedByDriver:{driverId.Value}" + (string.IsNullOrWhiteSpace(request.Notes) ? "" : $" | {request.Notes}"),
            null
        )), cancellationToken);

        if (!create.Success) return create;
        return ApiResponse<int>.SuccessResponse(create.Data, "Payment collected successfully.");
    }
}
