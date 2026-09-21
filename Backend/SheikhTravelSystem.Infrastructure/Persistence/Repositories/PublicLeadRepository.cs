using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class PublicLeadRepository(IDbConnectionFactory dbFactory) : IPublicLeadRepository
{
    public async Task InsertContactRequestAsync(
        string firstName,
        string lastName,
        string company,
        string email,
        string? phone,
        string? country,
        string? fleetSize,
        string? interestedIn,
        string message,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO WebsiteContactRequests
                (TenantId, FirstName, LastName, Company, Email, Phone, Country, FleetSize, InterestedIn, Message, Status)
            VALUES
                (1, @FirstName, @LastName, @Company, @Email, @Phone, @Country, @FleetSize, @InterestedIn, @Message, N'New')
            """,
            new
            {
                FirstName = firstName,
                LastName = lastName,
                Company = company,
                Email = email,
                Phone = phone,
                Country = country,
                FleetSize = fleetSize,
                InterestedIn = interestedIn,
                Message = message
            },
            cancellationToken: cancellationToken));
    }

    public async Task InsertDemoRequestAsync(
        string name,
        string company,
        string email,
        string? phone,
        string? country,
        string? vehicleCount,
        string? currentGpsProvider,
        string? interestedProduct,
        string? message,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO WebsiteDemoRequests
                (TenantId, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider, InterestedProduct, Message, Status)
            VALUES
                (1, @Name, @Company, @Email, @Phone, @Country, @VehicleCount, @CurrentGpsProvider, @InterestedProduct, @Message, N'New')
            """,
            new
            {
                Name = name,
                Company = company,
                Email = email,
                Phone = phone,
                Country = country,
                VehicleCount = vehicleCount,
                CurrentGpsProvider = currentGpsProvider,
                InterestedProduct = interestedProduct,
                Message = message
            },
            cancellationToken: cancellationToken));
    }
}
