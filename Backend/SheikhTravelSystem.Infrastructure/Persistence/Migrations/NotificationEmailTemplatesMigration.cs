using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds / upgrades branded HTML Email templates and professional subjects for SheikhGo.
/// </summary>
public static class NotificationEmailTemplatesMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            var templates = BuildTemplates();
            foreach (var t in templates)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF EXISTS (
                        SELECT 1 FROM NotificationTemplates
                        WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0)
                    BEGIN
                        UPDATE NotificationTemplates SET
                            TemplateName = @Name,
                            Subject = @Subject,
                            Body = @Body,
                            IsActive = 1,
                            Language = 'en',
                            Variables = @Variables,
                            UpdatedAt = GETUTCDATE()
                        WHERE TemplateKey = @Key AND Channel = @Channel AND IsDeleted = 0;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO NotificationTemplates
                            (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
                        VALUES
                            (@Key, @Name, @Subject, @Body, @Channel, 1, 'en', @Variables, GETUTCDATE(), 0);
                    END
                    """,
                    new
                    {
                        t.Key, t.Name, t.Subject, t.Body, t.Channel, t.Variables
                    },
                    cancellationToken: cancellationToken));
            }

            logger.LogInformation("NotificationEmailTemplatesMigration applied ({Count} templates).", templates.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationEmailTemplatesMigration failed.");
            throw;
        }
    }

    private static (string Key, string Name, string Subject, string Body, string Channel, string Variables)[] BuildTemplates()
    {
        const string varsCore = "[\"Title\",\"Message\",\"RecipientName\",\"VehicleName\",\"DriverName\",\"DateTime\",\"Location\",\"Priority\",\"PortalUrl\",\"CompanyName\"]";
        const string varsFleet = "[\"Title\",\"Message\",\"RecipientName\",\"VehicleName\",\"DriverName\",\"TrackerName\",\"Speed\",\"Battery\",\"FuelLevel\",\"DateTime\",\"Location\",\"Priority\",\"PortalUrl\"]";

        static string Frag(string intro) =>
            "<p style=\"color:#334155;line-height:1.55;margin:0 0 12px;\">" + intro + "</p>"
            + "<div style=\"background:#fff7ed;border-left:4px solid #f97316;padding:15px;border-radius:6px;color:#334155;line-height:1.55;\">"
            + "{{Message}}"
            + "</div>";

        return
        [
            ("sos_alert", "SOS Alert",
                "SOS Alert - {{Title}} | SheikhGo ERP",
                Frag("An SOS alert has been triggered and requires your immediate attention."),
                "Email", varsFleet),

            ("sos_alert", "SOS Alert",
                "SOS: {{Title}}",
                "{{Message}}",
                "Sms", "[\"Title\",\"Message\"]"),

            ("sos_alert", "SOS Alert",
                "SOS: {{Title}}",
                "{{Message}}",
                "InApp", "[\"Title\",\"Message\"]"),

            ("vehicle_offline", "Vehicle Offline",
                "Vehicle Offline - {{VehicleName}} | SheikhGo ERP",
                Frag("A vehicle has gone offline and may need investigation."),
                "Email", varsFleet),

            ("speed_alert", "Overspeed Alert",
                "Overspeed Alert - {{VehicleName}} | SheikhGo ERP",
                Frag("An overspeed event was detected. Please review driver behavior."),
                "Email", varsFleet),

            ("over_speed", "Overspeed Alert",
                "Overspeed Alert - {{VehicleName}} | SheikhGo ERP",
                Frag("An overspeed event was detected. Please review driver behavior."),
                "Email", varsFleet),

            ("fuel_alert", "Fuel Alert",
                "Fuel Alert - {{VehicleName}} | SheikhGo ERP",
                Frag("A fuel-related alert was raised for your fleet."),
                "Email", varsFleet),

            ("geofence_exit", "Geofence Exit",
                "Geofence Exit - {{VehicleName}} | SheikhGo ERP",
                Frag("A vehicle exited a monitored geofence."),
                "Email", varsFleet),

            ("maintenance_reminder", "Maintenance Reminder",
                "Maintenance Due - {{VehicleName}} | SheikhGo ERP",
                Frag("Scheduled maintenance is due. Please plan service to avoid downtime."),
                "Email", varsCore),

            ("compliance_reminder", "Compliance Reminder",
                "Compliance Reminder - {{Title}} | SheikhGo ERP",
                Frag("A compliance item requires attention (insurance, license, or document expiry)."),
                "Email", varsCore),

            ("insurance_expiry", "Insurance Expiry",
                "Insurance Expiring - {{VehicleName}} | SheikhGo ERP",
                Frag("Vehicle insurance is expiring soon. Renew to stay compliant."),
                "Email", varsCore),

            ("license_expiry", "Driver License Expiry",
                "License Expiring - {{DriverName}} | SheikhGo ERP",
                Frag("A driver license is expiring soon. Please renew before the deadline."),
                "Email", varsCore),

            ("booking_confirmation", "Booking Confirmation",
                "Booking Confirmed - {{Title}} | SheikhGo ERP",
                Frag("Your booking has been confirmed. Details are below."),
                "Email", varsCore),

            ("payment_received", "Payment Received",
                "Payment Received - {{Title}} | SheikhGo ERP",
                Frag("A payment was recorded successfully."),
                "Email", varsCore),

            ("ai_daily_summary", "AI Daily Summary",
                "Daily Fleet Summary | SheikhGo ERP",
                Frag("Here is your AI-generated daily fleet operations summary."),
                "Email", varsCore),

            ("welcome_email", "Welcome Email",
                "Welcome to SheikhGo ERP",
                Frag("Your SheikhGo ERP account is ready. Sign in to manage your fleet."),
                "Email", varsCore),

            ("password_reset", "Password Reset",
                "Reset your SheikhGo password",
                Frag("A password reset was requested for your account. Use the button below or open the portal to continue."),
                "Email", varsCore),

            ("system_notification", "System Notification",
                "{{Title}} | SheikhGo ERP",
                Frag("You have a new system notification."),
                "Email", varsCore),
        ];
    }
}
