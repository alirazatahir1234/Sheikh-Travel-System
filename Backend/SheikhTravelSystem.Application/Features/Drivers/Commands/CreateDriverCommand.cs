using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record CreateDriverCommand(CreateDriverDto Driver) : IRequest<ApiResponse<int>>;

public class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.Driver.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Driver.Phone).NotEmpty();
        RuleFor(x => x.Driver.LicenseNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Driver.LicenseExpiryDate).GreaterThan(DateTime.UtcNow)
            .WithMessage("License must not be expired.");
    }
}

public class CreateDriverCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateDriverCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Driver;

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE LicenseNumber = @License AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { License = dto.LicenseNumber });

        if (exists)
            throw new ConflictException($"Driver with license '{dto.LicenseNumber}' already exists.");

        var id = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Drivers (FullName, Phone, LicenseNumber, LicenseExpiryDate, CNIC, Address, Status, IsActive, CreatedAt, IsDeleted)
              VALUES (@FullName, @Phone, @LicenseNumber, @LicenseExpiryDate, @CNIC, @Address, @Status, 1, @CreatedAt, 0);
              SELECT SCOPE_IDENTITY();",
            new
            {
                dto.FullName, dto.Phone, dto.LicenseNumber, dto.LicenseExpiryDate,
                dto.CNIC, dto.Address, Status = (int)Domain.Enums.DriverStatus.Available,
                CreatedAt = DateTime.UtcNow
            });

        return ApiResponse<int>.SuccessResponse(id, "Driver created successfully.");
    }
}
