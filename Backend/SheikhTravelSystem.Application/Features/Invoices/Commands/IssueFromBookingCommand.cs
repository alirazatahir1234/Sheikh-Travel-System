using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Invoices.Commands;

public record IssueFromBookingCommand(int BookingId) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "CustomerInvoice";
    public int? AuditEntityId => null;
}

public class IssueFromBookingCommandValidator : AbstractValidator<IssueFromBookingCommand>
{
    public IssueFromBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
    }
}

public class IssueFromBookingCommandHandler(
    ICustomerInvoiceRepository invoiceRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<IssueFromBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(IssueFromBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var booking = await invoiceRepository.GetBookingSnapshotAsync(request.BookingId, tenantId, cancellationToken);
        if (booking is null)
            throw new NotFoundException("Booking", request.BookingId);

        if (booking.Status == (int)BookingStatus.Cancelled)
            return ApiResponse<int>.FailResponse("Cannot issue an invoice for a cancelled booking.");

        if (await invoiceRepository.HasActiveInvoiceForBookingAsync(request.BookingId, tenantId, cancellationToken))
            return ApiResponse<int>.FailResponse("An active invoice already exists for this booking.");

        var paid = await invoiceRepository.GetTotalPaidForBookingAsync(request.BookingId, tenantId, cancellationToken);
        if (paid > booking.TotalAmount)
            paid = booking.TotalAmount;

        var balance = booking.TotalAmount - paid;
        var status = balance <= 0 ? InvoiceStatus.Paid : InvoiceStatus.Issued;
        var number = await invoiceRepository.AllocateInvoiceNumberAsync(tenantId, cancellationToken);

        var id = await invoiceRepository.CreateAsync(
            tenantId,
            currentUser.UserId,
            booking,
            number,
            paid,
            balance,
            status,
            cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Invoice issued successfully.");
    }
}
