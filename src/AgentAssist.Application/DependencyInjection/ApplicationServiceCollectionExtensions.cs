using AgentAssist.Application.Abstractions;
using AgentAssist.Application.Assistant;
using AgentAssist.Application.Common;
using AgentAssist.Domain;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Application.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for the Application layer.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds Application layer services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IRequestHandler<AssistantQuery, Result<AssistantAnswer>>, AnswerAssistantQueryHandler>();
        services.AddScoped<IValidator<AssistantQuery>, AssistantQueryValidator>();

        return services;
    }
}
