using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>Logs OTP in production until Twilio/local SMS is configured.</summary>
public class ConsoleSmsOtpService(ILogger<ConsoleSmsOtpService> logger) : ISmsOtpService
{
    public Task SendOtpAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("SMS OTP to {Phone}: {Code}", phone, code);
        return Task.CompletedTask;
    }
}
