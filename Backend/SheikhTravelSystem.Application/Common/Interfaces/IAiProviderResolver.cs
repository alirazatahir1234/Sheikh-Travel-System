namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>Resolves the active LLM provider for a tenant (implementation in Infrastructure).</summary>
public interface IAiProviderResolver
{
    Task<(IAiProvider? Provider, AiProviderConfigDto Config)> ResolveAsync(
        int tenantId,
        CancellationToken cancellationToken = default);
}
