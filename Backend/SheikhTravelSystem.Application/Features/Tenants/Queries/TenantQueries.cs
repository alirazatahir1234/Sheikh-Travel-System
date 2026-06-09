using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Tenants.Queries;

public record GetTenantBrandingQuery : IRequest<ApiResponse<TenantBrandingDto>>;

public record UpdateTenantBrandingCommand(
    int TenantId,
    string? LogoUrl,
    string? PrimaryColor,
    string? Website,
    string? SupportEmail,
    string? Country,
    string? CurrencyCode,
    string? TimeZone) : IRequest<ApiResponse<bool>>;

public record TenantBrandingDto(
    int Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    IReadOnlyList<string> EnabledModules);

public class GetTenantBrandingQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<GetTenantBrandingQuery, ApiResponse<TenantBrandingDto>>
{
    public async Task<ApiResponse<TenantBrandingDto>> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Name, string Slug, string? LogoUrl, string? PrimaryColor)>(
            new CommandDefinition("""
                SELECT t.Id, t.Name, t.Slug, COALESCE(b.LogoUrl, t.LogoUrl) AS LogoUrl, COALESCE(b.PrimaryColor, t.PrimaryColor) AS PrimaryColor
                FROM Tenants t
                LEFT JOIN TenantBranding b ON b.TenantId = t.Id
                WHERE t.Id = @Id AND t.IsActive = 1
                """,
                new { Id = tenantId },
                cancellationToken: cancellationToken));

        if (row.Id == 0)
            return ApiResponse<TenantBrandingDto>.FailResponse("Tenant not found.");

        var modules = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);
        return ApiResponse<TenantBrandingDto>.SuccessResponse(
            new TenantBrandingDto(row.Id, row.Name, row.Slug, row.LogoUrl, row.PrimaryColor, modules));
    }
}

public class UpdateTenantBrandingCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope)
    : IRequestHandler<UpdateTenantBrandingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Tenants WHERE Id = @Id) THEN 1 ELSE 0 END",
            new { Id = request.TenantId }, cancellationToken: cancellationToken));
        if (!exists) throw new NotFoundException("Tenant", request.TenantId);

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM TenantBranding WHERE TenantId = @TenantId)
                UPDATE TenantBranding
                SET LogoUrl = @LogoUrl, PrimaryColor = @PrimaryColor, Website = @Website,
                    SupportEmail = @SupportEmail, Country = @Country, CurrencyCode = @CurrencyCode, TimeZone = @TimeZone
                WHERE TenantId = @TenantId;
            ELSE
                INSERT INTO TenantBranding (TenantId, LogoUrl, PrimaryColor, Website, SupportEmail, Country, CurrencyCode, TimeZone)
                VALUES (@TenantId, @LogoUrl, @PrimaryColor, @Website, @SupportEmail, @Country, @CurrencyCode, @TimeZone);
            """, new
        {
            request.TenantId,
            request.LogoUrl,
            request.PrimaryColor,
            request.Website,
            request.SupportEmail,
            request.Country,
            request.CurrencyCode,
            request.TimeZone
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Tenants SET LogoUrl = @LogoUrl, PrimaryColor = @PrimaryColor, UpdatedAt = GETUTCDATE()
            WHERE Id = @TenantId
            """, new
        {
            request.TenantId,
            request.LogoUrl,
            request.PrimaryColor
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Tenant branding updated.");
    }
}
