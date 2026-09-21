namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IPublicLeadRepository
{
    Task InsertContactRequestAsync(
        string firstName,
        string lastName,
        string company,
        string email,
        string? phone,
        string? country,
        string? fleetSize,
        string? interestedIn,
        string message,
        CancellationToken cancellationToken = default);

    Task InsertDemoRequestAsync(
        string name,
        string company,
        string email,
        string? phone,
        string? country,
        string? vehicleCount,
        string? currentGpsProvider,
        string? interestedProduct,
        string? message,
        CancellationToken cancellationToken = default);
}
