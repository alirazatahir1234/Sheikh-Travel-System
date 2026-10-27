using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Invoices.Services;

public interface IInvoiceIssueService
{
    Task<ApiResponse<int>> IssueFromBookingAsync(
        int tenantId, int? userId, int bookingId, CancellationToken cancellationToken = default);

    Task<ApiResponse<int>> IssueFromTripAsync(
        int tenantId, int? userId, int tripId, CancellationToken cancellationToken = default);
}

public sealed class InvoiceIssueService(ICustomerInvoiceRepository invoiceRepository) : IInvoiceIssueService
{
    public async Task<ApiResponse<int>> IssueFromBookingAsync(
        int tenantId, int? userId, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await invoiceRepository.GetBookingSnapshotAsync(bookingId, tenantId, cancellationToken);
        if (booking is null)
            return ApiResponse<int>.FailResponse("Booking not found.");

        if (booking.Status == (int)BookingStatus.Cancelled)
            return ApiResponse<int>.FailResponse("Cannot issue an invoice for a cancelled booking.");

        if (await invoiceRepository.HasActiveInvoiceForBookingAsync(bookingId, tenantId, cancellationToken))
            return ApiResponse<int>.FailResponse("An active invoice already exists for this booking.");

        var paid = await invoiceRepository.GetTotalPaidForBookingAsync(bookingId, tenantId, cancellationToken);
        if (paid > booking.TotalAmount)
            paid = booking.TotalAmount;

        return await CreateInvoiceAsync(
            tenantId,
            userId,
            new InvoiceCreatePayload
            {
                BookingId = booking.BookingId,
                CustomerId = booking.CustomerId,
                TotalAmount = booking.TotalAmount,
                CustomerName = booking.CustomerName,
                CustomerEmail = booking.CustomerEmail,
                BookingNumber = booking.BookingNumber,
                RouteName = booking.RouteName
            },
            paid,
            cancellationToken);
    }

    public async Task<ApiResponse<int>> IssueFromTripAsync(
        int tenantId, int? userId, int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await invoiceRepository.GetTripSnapshotAsync(tripId, tenantId, cancellationToken);
        if (trip is null)
            return ApiResponse<int>.FailResponse("Trip not found.");

        if (trip.Status == (int)TripStatus.Cancelled || trip.Status == (int)TripStatus.Failed)
            return ApiResponse<int>.FailResponse("Cannot issue an invoice for a cancelled or failed trip.");

        if (await invoiceRepository.HasActiveInvoiceForTripAsync(tripId, tenantId, cancellationToken))
            return ApiResponse<int>.FailResponse("An active invoice already exists for this trip.");

        if (trip.BookingId is int bookingId
            && await invoiceRepository.HasActiveInvoiceForBookingAsync(bookingId, tenantId, cancellationToken))
            return ApiResponse<int>.FailResponse("An active invoice already exists for the linked booking.");

        if (trip.TotalAmount <= 0)
            return ApiResponse<int>.FailResponse("Trip has no billable amount. Link a booking or route price first.");

        var paid = trip.BookingId is int linkedBooking
            ? await invoiceRepository.GetTotalPaidForBookingAsync(linkedBooking, tenantId, cancellationToken)
            : 0m;
        if (paid > trip.TotalAmount)
            paid = trip.TotalAmount;

        return await CreateInvoiceAsync(
            tenantId,
            userId,
            new InvoiceCreatePayload
            {
                BookingId = trip.BookingId,
                TripId = trip.TripId,
                CustomerId = trip.CustomerId,
                TotalAmount = trip.TotalAmount,
                CustomerName = trip.CustomerName,
                CustomerEmail = trip.CustomerEmail,
                BookingNumber = trip.BookingNumber,
                TripNumber = trip.TripNumber,
                RouteName = trip.RouteName
            },
            paid,
            cancellationToken);
    }

    private async Task<ApiResponse<int>> CreateInvoiceAsync(
        int tenantId,
        int? userId,
        InvoiceCreatePayload payload,
        decimal paidAmount,
        CancellationToken cancellationToken)
    {
        var balance = payload.TotalAmount - paidAmount;
        var status = balance <= 0 ? InvoiceStatus.Paid : InvoiceStatus.Pending;
        var issuedAt = DateTime.UtcNow;
        var dueDate = InvoiceLifecycleHelper.DefaultDueDateUtc(issuedAt);
        var number = await invoiceRepository.AllocateInvoiceNumberAsync(tenantId, cancellationToken);

        var id = await invoiceRepository.CreateAsync(
            tenantId,
            userId,
            payload,
            number,
            paidAmount,
            balance,
            status,
            issuedAt,
            dueDate,
            cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Invoice issued successfully.");
    }
}
