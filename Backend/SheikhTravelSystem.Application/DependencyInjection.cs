using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SheikhTravelSystem.Application.Common.Behaviors;

namespace SheikhTravelSystem.Application;

/// <summary>
/// Registers application-layer services, handlers, validators, and pipeline behaviors.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds MediatR, FluentValidation, and validation pipeline behavior.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
