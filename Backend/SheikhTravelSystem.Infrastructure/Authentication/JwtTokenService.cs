using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Infrastructure.Authentication;

/// <summary>
/// Generates signed JWT access tokens and secure refresh tokens.
/// </summary>
public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    /// <summary>
    /// Builds an access token from configured JWT settings and user claims.
    /// </summary>
    public string GenerateAccessToken(User user, int? driverId = null, UserAccessContext? access = null)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured.")));

        var roleCodes = access?.RoleCodes ?? [];
        var primaryRole = roleCodes.FirstOrDefault() ?? user.Role.ToString();
        var legacyRole = user.Role.ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, legacyRole),
            new("userId", user.Id.ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new("primary_role", primaryRole)
        };

        if (!string.Equals(primaryRole, legacyRole, StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, primaryRole));
        }

        foreach (var roleCode in roleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("role", roleCode));
        }

        foreach (var permission in access?.Permissions ?? [])
        {
            claims.Add(new Claim("permission", permission));
        }

        if (driverId.HasValue)
        {
            claims.Add(new Claim("driver_id", driverId.Value.ToString()));
        }

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GeneratePortalAccessToken(string phone, string fullName, int tenantId, int? customerId = null)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var portalSettings = configuration.GetSection("PortalAuth");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured.")));

        var claims = new List<Claim>
        {
            new(ClaimTypes.MobilePhone, phone.Trim()),
            new("portal_phone", phone.Trim()),
            new(ClaimTypes.Name, fullName.Trim()),
            new(ClaimTypes.Role, "PortalCustomer"),
            new("tenant_id", tenantId.ToString())
        };

        if (customerId.HasValue)
        {
            claims.Add(new Claim("portal_customer_id", customerId.Value.ToString()));
        }

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.TryParse(portalSettings["PortalTokenExpiryMinutes"], out var mins) && mins > 0
            ? mins
            : 10_080;

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateDriverAccessToken(int driverId, int userId, int tenantId, string fullName, string phone)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured.")));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("userId", userId.ToString()),
            new("driver_id", driverId.ToString()),
            new(ClaimTypes.Name, fullName.Trim()),
            new(ClaimTypes.MobilePhone, phone.Trim()),
            new(ClaimTypes.Role, "Driver"),
            new("tenant_id", tenantId.ToString())
        };

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "480");

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates a random, high-entropy token suitable for refresh workflows.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
