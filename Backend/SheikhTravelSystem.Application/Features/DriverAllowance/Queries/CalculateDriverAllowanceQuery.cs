using System.Globalization;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Queries;

/// <summary>
/// Evaluates the configured allowance rules against a booking context and
/// returns a single authoritative amount plus full traceability metadata.
///
/// The evaluator is intentionally keep-simple:
///   1. Pull candidate context (route distance, vehicle fuel type).
///   2. Load active rules ordered by priority (ascending) then id.
///   3. First rule whose filters all pass wins.
///   4. Amount is computed by the strategy implied by `CalculationType`.
///
/// When no rule applies (e.g. nothing configured), the evaluator returns
/// `Amount = 0` with `MatchedAnyRule = false` so callers can fall back to
/// manual entry without hidden defaults.
/// </summary>
public record CalculateDriverAllowanceQuery(CalculateDriverAllowanceRequest Request)
    : IRequest<ApiResponse<CalculateDriverAllowanceResponse>>;

public class CalculateDriverAllowanceQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CalculateDriverAllowanceQuery, ApiResponse<CalculateDriverAllowanceResponse>>
{
    public async Task<ApiResponse<CalculateDriverAllowanceResponse>> Handle(
        CalculateDriverAllowanceQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (req.RouteId <= 0)
            return ApiResponse<CalculateDriverAllowanceResponse>.FailResponse("Route is required for allowance calculation.");
        if (req.VehicleId <= 0)
            return ApiResponse<CalculateDriverAllowanceResponse>.FailResponse("Vehicle is required for allowance calculation.");

        using var connection = dbFactory.CreateConnection();

        var route = await connection.QuerySingleOrDefaultAsync<RouteContext>(
            new CommandDefinition(
                "SELECT Distance, Name, Source, Destination FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                new { Id = req.RouteId },
                cancellationToken: cancellationToken));

        if (route is null)
        {
            return ApiResponse<CalculateDriverAllowanceResponse>.SuccessResponse(
                new CalculateDriverAllowanceResponse(
                    Amount: 0,
                    AppliedRuleId: null,
                    AppliedRuleName: null,
                    AppliedRuleType: null,
                    AppliedRuleValue: null,
                    FormulaExplanation: "Route not found; allowance set to 0.",
                    MatchedAnyRule: false),
                "Route not found for allowance calculation.");
        }

        var vehicleFuelType = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT FuelType FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = req.VehicleId },
                cancellationToken: cancellationToken));

        if (vehicleFuelType is null)
        {
            return ApiResponse<CalculateDriverAllowanceResponse>.SuccessResponse(
                new CalculateDriverAllowanceResponse(
                    Amount: 0,
                    AppliedRuleId: null,
                    AppliedRuleName: null,
                    AppliedRuleType: null,
                    AppliedRuleValue: null,
                    FormulaExplanation: "Vehicle not found; allowance set to 0.",
                    MatchedAnyRule: false),
                "Vehicle not found for allowance calculation.");
        }

        var rules = (await connection.QueryAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, CalculationType, Value, Priority,
                         MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                         IsActive, Notes, CreatedAt
                  FROM DriverAllowanceRules
                  WHERE IsDeleted = 0 AND IsActive = 1
                  ORDER BY Priority ASC, Id ASC",
                cancellationToken: cancellationToken))).ToList();

        var distance = route.Distance;
        var routeHaystack = string.Join(' ', new[] { route.Name, route.Source, route.Destination }
            .Where(s => !string.IsNullOrWhiteSpace(s))).ToLowerInvariant();

        var match = rules.FirstOrDefault(r => IsApplicable(r, distance, (FuelType)vehicleFuelType.Value, routeHaystack));

        if (match is null)
        {
            return ApiResponse<CalculateDriverAllowanceResponse>.SuccessResponse(
                new CalculateDriverAllowanceResponse(
                    Amount: 0,
                    AppliedRuleId: null,
                    AppliedRuleName: null,
                    AppliedRuleType: null,
                    AppliedRuleValue: null,
                    FormulaExplanation: "No active rule matched — manual allowance required.",
                    MatchedAnyRule: false),
                "No active allowance rule matched.");
        }

        var (amount, explanation) = Apply(match, distance, req.TripDays ?? 1, req.Profit ?? 0m);

        return ApiResponse<CalculateDriverAllowanceResponse>.SuccessResponse(
            new CalculateDriverAllowanceResponse(
                Amount: Math.Round(amount, 2),
                AppliedRuleId: match.Id,
                AppliedRuleName: match.Name,
                AppliedRuleType: match.CalculationType,
                AppliedRuleValue: match.Value,
                FormulaExplanation: explanation,
                MatchedAnyRule: true),
            "Driver allowance calculated.");
    }

    private static bool IsApplicable(
        DriverAllowanceRuleDto rule, decimal distance, FuelType fuelType, string routeHaystack)
    {
        if (rule.MinDistanceKm.HasValue && distance < rule.MinDistanceKm.Value) return false;
        if (rule.MaxDistanceKm.HasValue && distance > rule.MaxDistanceKm.Value) return false;
        if (rule.VehicleFuelType.HasValue && rule.VehicleFuelType.Value != fuelType) return false;

        if (!string.IsNullOrWhiteSpace(rule.RouteFilter)
            && !routeHaystack.Contains(rule.RouteFilter.Trim().ToLowerInvariant()))
        {
            return false;
        }

        return true;
    }

    private sealed class RouteContext
    {
        public decimal Distance { get; set; }
        public string? Name { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
    }

    private static (decimal amount, string explanation) Apply(
        DriverAllowanceRuleDto rule, decimal distance, int tripDays, decimal profit)
    {
        var inv = CultureInfo.InvariantCulture;

        return rule.CalculationType switch
        {
            AllowanceCalculationType.FixedAmount =>
                (rule.Value, $"Fixed amount PKR {rule.Value.ToString("F2", inv)}"),

            AllowanceCalculationType.PerKm =>
                (rule.Value * distance,
                 $"{rule.Value.ToString("F2", inv)} PKR/km × {distance.ToString("F1", inv)} km = PKR {(rule.Value * distance).ToString("F2", inv)}"),

            AllowanceCalculationType.PerDay =>
                (rule.Value * Math.Max(1, tripDays),
                 $"{rule.Value.ToString("F2", inv)} PKR/day × {Math.Max(1, tripDays)} day(s) = PKR {(rule.Value * Math.Max(1, tripDays)).ToString("F2", inv)}"),

            AllowanceCalculationType.ProfitPercent =>
                (profit * (rule.Value / 100m),
                 $"{rule.Value.ToString("F2", inv)}% × profit PKR {profit.ToString("F2", inv)} = PKR {(profit * (rule.Value / 100m)).ToString("F2", inv)}"),

            _ => (0m, "Unknown calculation type — returning 0.")
        };
    }
}
