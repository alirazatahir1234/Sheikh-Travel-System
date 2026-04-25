using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

public record UpdateCustomerCommand(int Id, UpdateCustomerDto Customer) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Customer";
    public int? AuditEntityId => Id;
}

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Customer.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Customer.Phone).NotEmpty();
    }
}

public class UpdateCustomerCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateCustomerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Customer;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Customer", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Customers SET FullName = @FullName, Phone = @Phone, Email = @Email,
                  Address = @Address, CNIC = @CNIC, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { dto.FullName, dto.Phone, dto.Email, dto.Address, dto.CNIC, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Customer updated successfully.");
    }
}
