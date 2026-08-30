using System.Net;
using System.Text;
using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Application.Features.PublicLeads.Commands;

public record SubmitContactLeadCommand(
    string FirstName,
    string LastName,
    string Company,
    string Email,
    string Message,
    string? Phone = null,
    string? Country = null,
    string? FleetSize = null,
    string? InterestedIn = null,
    string? Website = null) : IRequest<ApiResponse<object>>;

public class SubmitContactLeadCommandValidator : AbstractValidator<SubmitContactLeadCommand>
{
    public SubmitContactLeadCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Company).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Country).MaximumLength(80);
        RuleFor(x => x.FleetSize).MaximumLength(80);
        RuleFor(x => x.InterestedIn).MaximumLength(120);
    }
}

public class SubmitContactLeadCommandHandler(
    IDbConnectionFactory dbFactory,
    IEnumerable<INotificationChannelSender> channelSenders,
    IConfiguration configuration,
    ILogger<SubmitContactLeadCommandHandler> logger)
    : IRequestHandler<SubmitContactLeadCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(SubmitContactLeadCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
            return ApiResponse<object>.SuccessResponse(new { }, "Message received.");

        using (var connection = dbFactory.CreateConnection())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteContactRequests
                    (TenantId, FirstName, LastName, Company, Email, Phone, Country, FleetSize, InterestedIn, Message, Status)
                VALUES
                    (1, @FirstName, @LastName, @Company, @Email, @Phone, @Country, @FleetSize, @InterestedIn, @Message, N'New')
                """,
                new
                {
                    request.FirstName,
                    request.LastName,
                    request.Company,
                    request.Email,
                    request.Phone,
                    request.Country,
                    request.FleetSize,
                    request.InterestedIn,
                    request.Message
                },
                cancellationToken: cancellationToken));
        }

        await MarketingLeadEmail.SendAsync(
            channelSenders,
            configuration,
            logger,
            $"SheikhGo contact — {request.Company}",
            "Website contact form",
            new Dictionary<string, string?>
            {
                ["Name"] = $"{request.FirstName} {request.LastName}".Trim(),
                ["Company"] = request.Company,
                ["Email"] = request.Email,
                ["Phone"] = request.Phone,
                ["Country"] = request.Country,
                ["Fleet size"] = request.FleetSize,
                ["Interested in"] = request.InterestedIn,
                ["Message"] = request.Message,
            },
            cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { }, "Message received.");
    }
}

public record SubmitDemoLeadCommand(
    string Name,
    string Company,
    string Email,
    string? Phone = null,
    string? Country = null,
    string? VehicleCount = null,
    string? CurrentGpsProvider = null,
    string? InterestedProduct = null,
    string? Message = null,
    string? Website = null) : IRequest<ApiResponse<object>>;

public class SubmitDemoLeadCommandValidator : AbstractValidator<SubmitDemoLeadCommand>
{
    public SubmitDemoLeadCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Company).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Country).MaximumLength(80);
        RuleFor(x => x.VehicleCount).MaximumLength(40);
        RuleFor(x => x.CurrentGpsProvider).MaximumLength(120);
        RuleFor(x => x.InterestedProduct).MaximumLength(120);
        RuleFor(x => x.Message).MaximumLength(4000);
    }
}

public class SubmitDemoLeadCommandHandler(
    IDbConnectionFactory dbFactory,
    IEnumerable<INotificationChannelSender> channelSenders,
    IConfiguration configuration,
    ILogger<SubmitDemoLeadCommandHandler> logger)
    : IRequestHandler<SubmitDemoLeadCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(SubmitDemoLeadCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
            return ApiResponse<object>.SuccessResponse(new { }, "Demo request received.");

        using (var connection = dbFactory.CreateConnection())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteDemoRequests
                    (TenantId, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider, InterestedProduct, Message, Status)
                VALUES
                    (1, @Name, @Company, @Email, @Phone, @Country, @VehicleCount, @CurrentGpsProvider, @InterestedProduct, @Message, N'New')
                """,
                new
                {
                    request.Name,
                    request.Company,
                    request.Email,
                    request.Phone,
                    request.Country,
                    request.VehicleCount,
                    request.CurrentGpsProvider,
                    request.InterestedProduct,
                    request.Message
                },
                cancellationToken: cancellationToken));
        }

        await MarketingLeadEmail.SendAsync(
            channelSenders,
            configuration,
            logger,
            $"SheikhGo demo request — {request.Company}",
            "Website demo request",
            new Dictionary<string, string?>
            {
                ["Name"] = request.Name,
                ["Company"] = request.Company,
                ["Email"] = request.Email,
                ["Phone"] = request.Phone,
                ["Country"] = request.Country,
                ["Vehicles"] = request.VehicleCount,
                ["Current GPS"] = request.CurrentGpsProvider,
                ["Product"] = request.InterestedProduct,
                ["Message"] = request.Message,
            },
            cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { }, "Demo request received.");
    }
}

internal static class MarketingLeadEmail
{
    public static async Task SendAsync(
        IEnumerable<INotificationChannelSender> channelSenders,
        IConfiguration configuration,
        ILogger logger,
        string subject,
        string heading,
        IReadOnlyDictionary<string, string?> fields,
        CancellationToken cancellationToken)
    {
        var to = configuration["Marketing:SalesEmail"]
            ?? configuration["Notifications:Email:DefaultTo"]
            ?? configuration["Notifications:Email:FromAddress"]
            ?? "info@sheikhgo.com";

        var sender = channelSenders.FirstOrDefault(s =>
            string.Equals(s.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase));
        if (sender is null)
        {
            logger.LogWarning("Marketing lead received but no email channel sender is registered: {Subject}", subject);
            return;
        }

        var html = BuildHtml(subject, heading, fields);
        var result = await sender.SendAsync(
            new ChannelSendRequest(0, null, null, subject, html, NotificationChannels.Email, Email: to),
            cancellationToken);

        if (!result.Success)
            logger.LogWarning("Marketing lead email failed ({Subject}): {Detail}", subject, result.Response);
    }

    private static string BuildHtml(string title, string heading, IReadOnlyDictionary<string, string?> fields)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><body style=\"font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:24px;\">");
        sb.Append("<div style=\"max-width:640px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;\">");
        sb.Append("<div style=\"background:#0F766E;color:#fff;padding:20px;font-weight:700;\">")
            .Append(WebUtility.HtmlEncode(title)).Append("</div>");
        sb.Append("<div style=\"padding:20px;\">");
        sb.Append("<p style=\"margin:0 0 12px;color:#334155;\">").Append(WebUtility.HtmlEncode(heading)).Append("</p>");
        sb.Append("<table cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:collapse;\">");
        foreach (var kv in fields)
        {
            if (string.IsNullOrWhiteSpace(kv.Value)) continue;
            sb.Append("<tr>")
                .Append("<td style=\"padding:6px 0;color:#64748b;width:140px;vertical-align:top;\">")
                .Append(WebUtility.HtmlEncode(kv.Key))
                .Append("</td>")
                .Append("<td style=\"padding:6px 0;color:#0f172a;\">")
                .Append(WebUtility.HtmlEncode(kv.Value).Replace("\n", "<br/>", StringComparison.Ordinal))
                .Append("</td></tr>");
        }
        sb.Append("</table></div></div></body></html>");
        return sb.ToString();
    }
}
