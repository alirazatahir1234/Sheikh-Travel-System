using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Flips INVOICES module from ComingSoon → Active with ERP route /invoices.
/// </summary>
public static class ModuleInvoicesActiveMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID('Modules', 'U') IS NULL RETURN;

            UPDATE Modules
            SET Status = N'Active', Route = N'/invoices'
            WHERE ModuleCode = N'INVOICES';
            """, cancellationToken: cancellationToken));

        logger.LogInformation("ModuleInvoicesActiveMigration applied (INVOICES → Active /invoices).");
    }
}
