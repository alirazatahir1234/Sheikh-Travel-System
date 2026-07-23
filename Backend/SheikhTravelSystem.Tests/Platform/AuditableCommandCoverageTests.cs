using FluentAssertions;
using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Reflection;

namespace SheikhTravelSystem.Tests.Platform;

public class AuditableCommandCoverageTests
{
    private static readonly string[] FeatureFolders =
    [
        "Bookings", "Trips", "Customers", "Payments", "Vehicles", "Drivers", "FuelLogs", "Routes", "Users"
    ];

    [Fact]
    public void Erp_mutation_commands_implement_IAuditableCommand()
    {
        var assembly = typeof(IAuditableCommand).Assembly;
        var missing = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;
            if (!ImplementsIRequest(type)) continue;
            if (!type.Name.EndsWith("Command", StringComparison.Ordinal)) continue;
            if (type.Name.EndsWith("Validator", StringComparison.Ordinal)) continue;

            var ns = type.Namespace ?? "";
            if (!FeatureFolders.Any(f => ns.Contains($".Features.{f}", StringComparison.Ordinal)))
                continue;

            // Skip query-shaped types accidentally named Command outside Commands folders is fine —
            // still require audit if they mutate via IRequest and live under Features.
            if (typeof(IAuditableCommand).IsAssignableFrom(type))
                continue;

            missing.Add($"{type.FullName}");
        }

        missing.Should().BeEmpty(
            "ERP mutation commands must implement IAuditableCommand. Missing: {0}",
            string.Join(", ", missing));
    }

    private static bool ImplementsIRequest(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
    }
}
