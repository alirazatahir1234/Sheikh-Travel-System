namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>Protects/unprotects opaque trip tracking tokens (implementation uses Data Protection).</summary>
public interface ITrackingTokenProtector
{
    string Protect(string plainToken);
    string Unprotect(string protectedToken);
}
