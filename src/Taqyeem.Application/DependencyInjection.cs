using Microsoft.Extensions.DependencyInjection;
using Taqyeem.Application.Quota;
using Taqyeem.Application.Routing;
using Taqyeem.Application.Scoring;
using Taqyeem.Application.Services;

namespace Taqyeem.Application;

public static class DependencyInjection
{
    /// <summary>Registers the stateless business-rule engines and application services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IScoringEngine, ScoringEngine>();
        services.AddSingleton<IRoutingEngine, RoutingEngine>();
        services.AddSingleton<IQuotaEngine, QuotaEngine>();

        services.AddScoped<PersonaService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<EvaluationService>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<CalibrationService>();
        return services;
    }
}
