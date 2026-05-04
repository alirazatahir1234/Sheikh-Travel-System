using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class UpdatePaymentStatusCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdatePaymentStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdatePaymentStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Payments WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Payment", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Payments SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Status = (int)request.Status, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, $"Payment status updated to {request.Status}.");
    }
}
