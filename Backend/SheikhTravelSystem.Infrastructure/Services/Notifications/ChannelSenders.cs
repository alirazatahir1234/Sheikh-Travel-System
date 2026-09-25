using Dapper;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

public sealed class EmailNotificationSender(
    IConfiguration configuration,
    IDbConnectionFactory dbFactory,
    ILogger<EmailNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.Email;

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Notifications:Email");
        var enabled = section.GetValue("Enabled", false);
        var host = section.GetValue<string>("SmtpHost");
        var user = section.GetValue<string>("Username");
        var pass = section.GetValue<string>("Password");

        var to = request.Email;
        if (string.IsNullOrWhiteSpace(to) && request.UserId is int uid)
        {
            using var connection = dbFactory.CreateConnection();
            to = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT Email FROM Users WHERE Id = @Id AND IsDeleted = 0 AND TenantId = @TenantId",
                new { Id = uid, TenantId = request.TenantId ?? 1 }, cancellationToken: cancellationToken));
        }
        to ??= section.GetValue<string>("DefaultTo");

        if (!enabled || string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "Email notification {Id} queued (SMTP not configured): {Title} → {Email}",
                request.NotificationId, request.Title, to ?? "(user)");
            return new ChannelSendResult(true, "Sent", "Logged (SMTP not configured — console mode)", "SmtpConsole");
        }

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return new ChannelSendResult(false, "Failed", "SMTP Username/Password missing", "Smtp");

        if (string.IsNullOrWhiteSpace(to))
            return new ChannelSendResult(false, "Failed", "No recipient email address", "Smtp");

        try
        {
            var port = section.GetValue("SmtpPort", 587);
            var from = section.GetValue<string>("FromAddress") ?? user ?? "noreply@sheikhgo.com";
            var enableSsl = section.GetValue("EnableSsl", true);

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(to));

            var cc = section.GetValue<string>("Cc");
            if (!string.IsNullOrWhiteSpace(cc))
                message.Cc.Add(MailboxAddress.Parse(cc));

            message.Subject = request.Title;

            // Prefer HTML (branded templates); fall back to plain text.
            if (EmailTemplateRenderer.IsHtmlDocument(request.Message)
                || EmailTemplateRenderer.LooksLikeHtmlFragment(request.Message))
            {
                message.Body = new TextPart("html") { Text = request.Message };
            }
            else
            {
                message.Body = new TextPart("plain") { Text = request.Message };
            }

            using var client = new SmtpClient();
            // SpaceMail: 587 = STARTTLS, 465 = SSL on connect
            var secure = !enableSsl
                ? SecureSocketOptions.None
                : port == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

            await client.ConnectAsync(host, port, secure, cancellationToken);
            await client.AuthenticateAsync(user!, pass!, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("SMTP ok notification {Id} → {Email}", request.NotificationId, to);
            return new ChannelSendResult(true, "Sent", $"SMTP ok → {to}", "Smtp");
        }
        catch (AuthenticationException ex)
        {
            logger.LogWarning(
                ex,
                "SMTP authentication failed for notification {Id} — check Notifications:Email Username/Password (host {Host}, port {Port}) or set Enabled=false for local dev",
                request.NotificationId, host, section.GetValue("SmtpPort", 587));
            return new ChannelSendResult(false, "Failed", ex.Message, "Smtp");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMTP send failed for notification {Id}", request.NotificationId);
            return new ChannelSendResult(false, "Failed", ex.Message, "Smtp");
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

        logger.LogWarning(
            "SMS provider {Provider} is selected but not fully wired; logging notification {Id}",
            provider, request.NotificationId);
        return Task.FromResult(new ChannelSendResult(true, "Sent", $"Queued for {provider} (stub)"));
    }
}

public sealed class PushNotificationSender(
    IConfiguration configuration,
    IDeviceTokenService deviceTokens,
    FcmHttpV1Client fcm,
    ILogger<PushNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.Push;

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue("Notifications:Push:Enabled", false);
        var projectId = configuration.GetValue<string>("Notifications:Push:ProjectId");
        var credentialsPath = configuration.GetValue<string>("Notifications:Push:CredentialsPath");

        IReadOnlyList<string> tokens = [];
        if (request.UserId is int uid)
            tokens = await deviceTokens.GetActiveTokensAsync(uid, request.TenantId, cancellationToken);

        if (!enabled || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(credentialsPath))
        {
            logger.LogInformation(
                "FCM push (stub) notification {Id}: {Title} → {TokenCount} device token(s)",
                request.NotificationId, request.Title, tokens.Count);
            return new ChannelSendResult(
                true, "Sent",
                tokens.Count == 0
                    ? "Logged (FCM not configured; no device tokens)"
                    : $"Logged (FCM stub) for {tokens.Count} token(s)",
                "FcmStub");
        }

        if (tokens.Count == 0)
            return new ChannelSendResult(false, "Failed", "No active device tokens for user", "Fcm");

        var (ok, detail) = await fcm.SendAsync(
            projectId!, credentialsPath!, tokens, request.Title, request.Message, cancellationToken);

        return ok
            ? new ChannelSendResult(true, "Sent", detail, "FcmHttpV1")
            : new ChannelSendResult(false, "Failed", detail, "FcmHttpV1");
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
            tenantId = request.TenantId ?? 1,
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
    IOptions<WhatsAppOptions> whatsAppOptions,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppInboxRepository inboxRepository,
    IWhatsAppTemplateRepository templateRepository,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppRoutingService routing,
    IWhatsAppAccountResolver accountResolver,
    ILogger<WhatsAppNotificationSender> logger) : INotificationChannelSender
{
    public string Channel => NotificationChannels.WhatsApp;

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue("WhatsApp:Enabled", false)
            || configuration.GetValue("Notifications:WhatsApp:Enabled", false);
        if (!enabled)
        {
            logger.LogInformation(
                "WhatsApp (disabled) notification {Id}: {Title} → {Phone}",
                request.NotificationId, request.Title, request.Phone ?? "(user)");
            return new ChannelSendResult(true, "Sent", "Logged (WhatsApp not configured)");
        }

        var phone = WhatsAppPhone.ToApiDigits(request.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return new ChannelSendResult(false, "Failed", "Recipient phone missing");

        // Prefer tenant from request when available; notifications often use tenant 1 accounts list.
        var accounts = await inboxRepository.ListActiveAccountRowsAsync(cancellationToken);
        var code = routing.ResolveAccountCode(request.Phone);
        var account = accounts.FirstOrDefault(a =>
            string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? await accountResolver.ResolveAsync(
                accounts.FirstOrDefault()?.TenantId ?? 1, null, request.Phone, cancellationToken);
        if (account is null)
            return new ChannelSendResult(false, "Failed", "No WhatsApp account configured");

        if (accountConfig.ResolveCredentials(account) is null)
            return new ChannelSendResult(false, "Failed", "WhatsApp credentials missing for account " + account.Code);

        var body = string.IsNullOrWhiteSpace(request.Message)
            ? request.Title
            : $"{request.Title}\n{request.Message}";

        var lastIncoming = await inboxRepository.GetLastIncomingMessageAtAsync(
            account.TenantId, account.Id, request.Phone!, cancellationToken);

        if (WhatsAppMessagingWindow.IsOpen(lastIncoming))
        {
            var result = await cloudApi.SendTextAsync(account, phone, body ?? "", cancellationToken);
            return result.Success
                ? new ChannelSendResult(true, "Sent", result.MetaMessageId)
                : new ChannelSendResult(false, "Failed", result.ErrorMessage);
        }

        var opts = whatsAppOptions.Value;
        var templateName = string.IsNullOrWhiteSpace(opts.NotificationTemplateName)
            ? null
            : opts.NotificationTemplateName.Trim();
        if (templateName is null)
        {
            logger.LogWarning(
                "WhatsApp notification {Id} skipped: messaging window closed and no NotificationTemplateName configured",
                request.NotificationId);
            return new ChannelSendResult(false, "Failed", "Template required: messaging window closed");
        }

        var language = string.IsNullOrWhiteSpace(opts.NotificationTemplateLanguage)
            ? "en"
            : opts.NotificationTemplateLanguage.Trim();
        var template = await templateRepository.GetByNameAsync(
            account.TenantId, account.Id, templateName, language, cancellationToken);
        if (template is null ||
            !string.Equals(template.Status, WhatsAppTemplateStatuses.Approved, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "WhatsApp notification {Id} failed: window closed and template '{Template}' is missing or not Approved",
                request.NotificationId, templateName);
            return new ChannelSendResult(false, "Failed",
                $"Template required: messaging window closed (catalog '{templateName}' not Approved)");
        }

        var templateResult = await cloudApi.SendTemplateAsync(
            account, phone, template.Name, language, null, cancellationToken);
        return templateResult.Success
            ? new ChannelSendResult(true, "Sent", templateResult.MetaMessageId)
            : new ChannelSendResult(false, "Failed", templateResult.ErrorMessage);
    }
}
