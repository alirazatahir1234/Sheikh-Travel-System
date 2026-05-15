using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

public record CreateCustomerCommand(CreateCustomerDto Customer) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Customer";
    public int? AuditEntityId => null;
}

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Customer.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Customer.Phone).NotEmpty();
        RuleFor(x => x.Customer.FatherOrHusbandName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Customer.FatherOrHusbandName));
        RuleFor(x => x.Customer.Gender).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Customer.Gender));
        RuleFor(x => x.Customer.Nationality).MaximumLength(120).When(x => !string.IsNullOrEmpty(x.Customer.Nationality));
    }
}

public class CreateCustomerCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateCustomerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Customer;

        if (!string.IsNullOrWhiteSpace(dto.CNIC))
        {
            var dup = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT TOP 1 Id FROM Customers WHERE CNIC = @CNIC AND IsDeleted = 0",
                    new { dto.CNIC },
                    cancellationToken: cancellationToken));
            if (dup.HasValue)
                return ApiResponse<int>.FailResponse("A customer with this CNIC already exists.");
        }

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Customers (FullName, Phone, Email, Address, CNIC, FatherOrHusbandName, Gender, DateOfBirth, Nationality, IsActive, CreatedAt, IsDeleted)
                  VALUES (@FullName, @Phone, @Email, @Address, @CNIC, @FatherOrHusbandName, @Gender, @DateOfBirth, @Nationality, 1, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
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
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Customer created successfully.");
    }
}
