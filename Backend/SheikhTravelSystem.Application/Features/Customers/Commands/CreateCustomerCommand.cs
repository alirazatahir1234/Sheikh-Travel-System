using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Commands;

public record CreateCustomerCommand(CreateCustomerDto Customer) : IRequest<ApiResponse<int>>;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Customer.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Customer.Phone).NotEmpty();
    }
}

public class CreateCustomerCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateCustomerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Customer;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Customers (FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt, IsDeleted)
                  VALUES (@FullName, @Phone, @Email, @Address, @CNIC, 1, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new { dto.FullName, dto.Phone, dto.Email, dto.Address, dto.CNIC, CreatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Customer created successfully.");
    }
}
