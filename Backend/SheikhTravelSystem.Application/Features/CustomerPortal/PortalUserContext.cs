using System.Security.Claims;

namespace SheikhTravelSystem.Application.Features.CustomerPortal;

public static class PortalUserContext
{
    public const string PortalRole = "PortalCustomer";

    public static string? GetPhone(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst("portal_phone")?.Value
            ?? user.FindFirst(ClaimTypes.MobilePhone)?.Value;
    }
}
