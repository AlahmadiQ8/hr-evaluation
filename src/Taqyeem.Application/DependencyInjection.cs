using Microsoft.Extensions.DependencyInjection;
using Taqyeem.Application.Quota;
using Taqyeem.Application.Routing;
using Taqyeem.Application.Scoring;

namespace Taqyeem.Application;

public static class DependencyInjection
{
    /// <summary>Registers the stateless business-rule engines.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IScoringEngine, ScoringEngine>();
        services.AddSingleton<IRoutingEngine, RoutingEngine>();
        services.AddSingleton<IQuotaEngine, QuotaEngine>();
        return services;
    }
}
