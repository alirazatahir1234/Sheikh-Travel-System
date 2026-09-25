using Microsoft.AspNetCore.DataProtection;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

public sealed class TrackingTokenProtector(IDataProtectionProvider provider) : ITrackingTokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("SheikhGo.TripTrackingToken.v1");

    public string Protect(string plainToken) => _protector.Protect(plainToken);

    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}
