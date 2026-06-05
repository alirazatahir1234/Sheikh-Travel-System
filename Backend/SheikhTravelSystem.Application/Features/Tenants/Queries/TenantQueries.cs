using System.Text.Json;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Tenants.Queries;

public record GetTenantBrandingQuery : IRequest<ApiResponse<TenantBrandingDto>>;

public record TenantBrandingDto(
    int Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    IReadOnlyList<string> EnabledModules);

public class GetTenantBrandingQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetTenantBrandingQuery, ApiResponse<TenantBrandingDto>>
{
    public async Task<ApiResponse<TenantBrandingDto>> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Name, string Slug, string? LogoUrl, string? PrimaryColor, string? EnabledModulesJson)>(
            new CommandDefinition(
                "SELECT Id, Name, Slug, LogoUrl, PrimaryColor, EnabledModulesJson FROM Tenants WHERE Id = @Id AND IsActive = 1",
                new { Id = tenantId },
                cancellationToken: cancellationToken));

        if (row.Id == 0)
            return ApiResponse<TenantBrandingDto>.FailResponse("Tenant not found.");

        var modules = ParseModules(row.EnabledModulesJson);
        return ApiResponse<TenantBrandingDto>.SuccessResponse(
            new TenantBrandingDto(row.Id, row.Name, row.Slug, row.LogoUrl, row.PrimaryColor, modules));
    }

    private static IReadOnlyList<string> ParseModules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public record ProvisionTenantCommand(string Name, string Slug, string AdminEmail, string AdminPassword, string AdminFullName)
    : IRequest<ApiResponse<int>>;

public class ProvisionTenantCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher)
    : IRequestHandler<ProvisionTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Tenants WHERE Slug = @Slug) THEN 1 ELSE 0 END",
            new { Slug = request.Slug.Trim().ToLowerInvariant() },
            cancellationToken: cancellationToken));

        if (exists)
            return ApiResponse<int>.FailResponse("Tenant slug already exists.");

        var tenantId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Tenants (Name, Slug, IsActive, EnabledModulesJson, CreatedAt)
              VALUES (@Name, @Slug, 1, @Modules, GETUTCDATE());
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                Name = request.Name.Trim(),
                Slug = request.Slug.Trim().ToLowerInvariant(),
                Modules = """["dashboard","bookings","vehicles","drivers","customers","routes","gps-tracking","payments","reports"]"""
            },
            cancellationToken: cancellationToken));

        var hash = passwordHasher.Hash(request.AdminPassword);
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Users (TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted)
              VALUES (@TenantId, @Name, @Email, @Hash, '', 1, 1, GETUTCDATE(), 0)",
            new
            {
                TenantId = tenantId,
                Name = request.AdminFullName.Trim(),
                Email = request.AdminEmail.Trim(),
                Hash = hash
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(tenantId, "Tenant provisioned.");
    }
}
