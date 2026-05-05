using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record CreatePortalPaymentCommand(int BookingId, CreatePortalPaymentRequest Request)
    : IRequest<ApiResponse<int>>;

public class CreatePortalPaymentCommandValidator : AbstractValidator<CreatePortalPaymentCommand>
{
    public CreatePortalPaymentCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Request.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.Amount).GreaterThan(0);
        RuleFor(x => x.Request.PaymentMethod).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.TransactionReference).MaximumLength(200);
        RuleFor(x => x.Request.Notes).MaximumLength(500);
    }
}

public class CreatePortalPaymentCommandHandler(IDbConnectionFactory dbFactory, ISender mediator)
    : IRequestHandler<CreatePortalPaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreatePortalPaymentCommand request, CancellationToken cancellationToken)
    {
        var phone = request.Request.Phone.Trim();
        using var connection = dbFactory.CreateConnection();

        var allowed = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings b
                    INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
                    WHERE b.Id = @BookingId AND b.IsDeleted = 0 AND c.Phone = @Phone) THEN 1 ELSE 0 END",
                new { request.BookingId, Phone = phone },
                cancellationToken: cancellationToken));

        if (!allowed)
            return ApiResponse<int>.FailResponse("Booking not found for this phone number.");

        return await mediator.Send(
            new CreatePaymentCommand(
                new CreatePaymentDto(
                    request.BookingId,
                    request.Request.Amount,
                    request.Request.PaymentMethod,
                    request.Request.TransactionReference,
                    request.Request.Notes,
                    null)),
            cancellationToken);
    }
}
