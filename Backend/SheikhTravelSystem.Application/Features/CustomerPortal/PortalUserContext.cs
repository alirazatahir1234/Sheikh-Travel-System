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

    public static int? GetCustomerId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var claim = user.FindFirst("portal_customer_id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
