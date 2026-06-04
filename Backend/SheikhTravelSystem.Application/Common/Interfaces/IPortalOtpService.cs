namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IPortalOtpService
{
    void Store(string phone, string code);

    bool TryValidate(string phone, string code, out string? error);
}
