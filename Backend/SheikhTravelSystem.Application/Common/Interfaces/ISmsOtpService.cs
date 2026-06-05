namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ISmsOtpService
{
    Task SendOtpAsync(string phone, string code, CancellationToken cancellationToken = default);
}
