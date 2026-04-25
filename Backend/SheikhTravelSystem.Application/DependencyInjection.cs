using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SheikhTravelSystem.Application.Common.Behaviors;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application;

/// <summary>
/// Registers application-layer services, handlers, validators, and pipeline behaviors.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds MediatR, FluentValidation, and pipeline behaviors.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(assembly);
        RegisterValidators(services, assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));

        return services;
    }

    private static void RegisterValidators(IServiceCollection services, Assembly assembly)
    {
        var validatorType = typeof(IValidator<>);
        var validators = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition
                && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType));

        foreach (var validator in validators)
        {
            foreach (var iface in validator.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorType))
            {
                services.AddTransient(iface, validator);
            }
        }
    }
}
