using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

public sealed class EmailNotificationSender(
    IConfiguration configuration,
    ILogger<EmailNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.Email;

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Notifications:Email");
        var enabled = section.GetValue("Enabled", false);
        var host = section.GetValue<string>("SmtpHost");

        if (!enabled || string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email notification {Id} queued (SMTP not configured): {Title} → {Email}",
                request.NotificationId, request.Title, request.Email ?? "(user)");
            return new ChannelSendResult(true, "Sent", "Logged (SMTP not configured — console mode)");
        }

        try
        {
            var port = section.GetValue("SmtpPort", 587);
            var user = section.GetValue<string>("Username");
            var pass = section.GetValue<string>("Password");
            var from = section.GetValue<string>("FromAddress") ?? user ?? "noreply@sheikhgo.com";
            var to = request.Email ?? section.GetValue<string>("DefaultTo");

            if (string.IsNullOrWhiteSpace(to))
                return new ChannelSendResult(false, "Failed", "No recipient email address");

            using var client = new System.Net.Mail.SmtpClient(host, port)
            {
                EnableSsl = section.GetValue("EnableSsl", true),
                Credentials = string.IsNullOrWhiteSpace(user)
                    ? null
                    : new System.Net.NetworkCredential(user, pass)
            };

            using var mail = new System.Net.Mail.MailMessage(from, to, request.Title, request.Message)
            {
                IsBodyHtml = request.Message.Contains('<')
            };

            var cc = section.GetValue<string>("Cc");
            if (!string.IsNullOrWhiteSpace(cc))
                mail.CC.Add(cc);

            await client.SendMailAsync(mail, cancellationToken);
            return new ChannelSendResult(true, "Sent", $"SMTP ok → {to}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMTP send failed for notification {Id}", request.NotificationId);
            return new ChannelSendResult(false, "Failed", ex.Message);
        }
    }
}

public sealed class SmsNotificationSender(
    IConfiguration configuration,
    ILogger<SmsNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.Sms;

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Notifications:Sms");
        var enabled = section.GetValue("Enabled", false);
        var provider = section.GetValue<string>("Provider") ?? "Console";

        if (!enabled || string.Equals(provider, "Console", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "SMS [{Provider}] notification {Id}: {Title} → {Phone}: {Message}",
                provider, request.NotificationId, request.Title, request.Phone ?? "(user)", request.Message);
            return Task.FromResult(new ChannelSendResult(true, "Sent", $"Logged via {provider}"));
        }

        // Twilio / Vonage hooks — credentials required; fall back to log until configured.
        logger.LogWarning(
            "SMS provider {Provider} is selected but not fully wired; logging notification {Id}",
            provider, request.NotificationId);
        return Task.FromResult(new ChannelSendResult(true, "Sent", $"Queued for {provider} (stub)"));
    }
}

public sealed class PushNotificationSender(
    IConfiguration configuration,
    ILogger<PushNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.Push;

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue("Notifications:Push:Enabled", false);
        if (!enabled)
        {
            logger.LogInformation(
                "FCM push (disabled) notification {Id}: {Title}", request.NotificationId, request.Title);
            return Task.FromResult(new ChannelSendResult(true, "Sent", "Logged (FCM not configured)"));
        }

        // Device tokens + FCM HTTP v1 can be wired when Firebase credentials are present.
        logger.LogInformation("FCM push stub for notification {Id}", request.NotificationId);
        return Task.FromResult(new ChannelSendResult(true, "Sent", "FCM stub accepted"));
    }
}

public sealed class BrowserNotificationSender(
    INotificationRealtimePublisher realtime,
    ILogger<BrowserNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.Browser;

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UserId is not int userId)
            return new ChannelSendResult(false, "Failed", "Browser channel requires UserId");

        await realtime.PublishToUserAsync(userId, new
        {
            kind = "browser",
            title = request.Title,
            message = request.Message,
            notificationId = request.NotificationId
        }, cancellationToken);

        logger.LogDebug("Browser notification pushed for user {UserId}", userId);
        return new ChannelSendResult(true, "Sent", "SignalR browser event published");
    }
}

public sealed class WhatsAppNotificationSender(
    IConfiguration configuration,
    ILogger<WhatsAppNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.WhatsApp;

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue("Notifications:WhatsApp:Enabled", false);
        if (!enabled)
        {
            logger.LogInformation(
                "WhatsApp (disabled) notification {Id}: {Title} → {Phone}",
                request.NotificationId, request.Title, request.Phone ?? "(user)");
            return Task.FromResult(new ChannelSendResult(true, "Sent", "Logged (WhatsApp not configured)"));
        }

        return Task.FromResult(new ChannelSendResult(true, "Sent", "WhatsApp stub accepted"));
    }
}
