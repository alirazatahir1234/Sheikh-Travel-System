using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Payments.Commands;

public record UpdatePaymentStatusCommand(int Id = 0, PaymentStatus Status = PaymentStatus.Pending)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction    => "UpdateStatus";
    public string AuditEntityName => "Payment";
    public int?   AuditEntityId   => Id;
}

public class UpdatePaymentStatusCommandValidator : AbstractValidator<UpdatePaymentStatusCommand>
{
    public UpdatePaymentStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class UpdatePaymentStatusCommandHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<UpdatePaymentStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdatePaymentStatusCommand request, CancellationToken cancellationToken)
    {
        var exists = await paymentRepository.ExistsAsync(request.Id, cancellationToken);
        if (!exists)
            throw new NotFoundException("Payment", request.Id);

        await paymentRepository.UpdateStatusAsync(request.Id, request.Status, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, $"Payment status updated to {request.Status}.");
    }
}
