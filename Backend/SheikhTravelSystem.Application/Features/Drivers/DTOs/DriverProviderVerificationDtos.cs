namespace SheikhTravelSystem.Application.Features.Drivers.DTOs;

public record DriverProviderVerificationCheckDto(
    int Id,
    string Provider,
    string DocumentType,
    string Status,
    string Summary,
    string? ExternalReference,
    int? HttpStatusCode,
    IReadOnlyList<string>? MissingConfigKeys,
    DateTime CheckedAt);

public record TriggerDriverProviderVerificationRequest(string? DocumentType = null);

public record DriverVerificationConfigStatusDto(
    bool NadraEnabled,
    IReadOnlyList<string> NadraMissingKeys,
    bool UaeKycEnabled,
    IReadOnlyList<string> UaeKycMissingKeys);
