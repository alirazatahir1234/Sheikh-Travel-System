using MediatR;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Settings;

/// <summary>Sidebar metadata describing a settings category in the control panel.</summary>
public record SettingsCategoryDto(
    string Id,
    string Label,
    string Icon,
    string Description,
    bool IsImplemented);

public record GetSettingsCategoriesQuery
    : IRequest<ApiResponse<IReadOnlyList<SettingsCategoryDto>>>;

/// <summary>Returns the current values for a category as a flat key/value map for the current tenant.</summary>
public record GetSettingsByCategoryQuery(string Category)
    : IRequest<ApiResponse<IReadOnlyDictionary<string, string?>>>;

/// <summary>Bulk-updates the values of a category for the current tenant.</summary>
public record UpdateSettingsCommand(string Category, IReadOnlyDictionary<string, string?> Values)
    : IRequest<ApiResponse<bool>>;
