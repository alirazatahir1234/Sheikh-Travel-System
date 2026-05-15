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
        RuleFor(x => x.Customer.FatherOrHusbandName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Customer.FatherOrHusbandName));
        RuleFor(x => x.Customer.Gender).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Customer.Gender));
        RuleFor(x => x.Customer.Nationality).MaximumLength(120).When(x => !string.IsNullOrEmpty(x.Customer.Nationality));
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

        if (!string.IsNullOrWhiteSpace(dto.CNIC))
        {
            var conflict = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    @"SELECT TOP 1 Id FROM Customers WHERE CNIC = @CNIC AND IsDeleted = 0 AND Id <> @Id",
                    new { dto.CNIC, request.Id },
                    cancellationToken: cancellationToken));
            if (conflict.HasValue)
                return ApiResponse<bool>.FailResponse("Another customer already uses this CNIC.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Customers SET FullName = @FullName, Phone = @Phone, Email = @Email,
                  Address = @Address, CNIC = @CNIC,
                  FatherOrHusbandName = @FatherOrHusbandName, Gender = @Gender, DateOfBirth = @DateOfBirth, Nationality = @Nationality,
                  UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new
                {
                    dto.FullName,
                    dto.Phone,
                    dto.Email,
                    dto.Address,
                    dto.CNIC,
                    dto.FatherOrHusbandName,
                    dto.Gender,
                    dto.DateOfBirth,
                    dto.Nationality,
                    UpdatedAt = DateTime.UtcNow,
                    request.Id
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Customer updated successfully.");
    }
}
