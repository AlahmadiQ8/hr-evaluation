using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Taqyeem.Infrastructure.Persistence;
using Taqyeem.Infrastructure.Seeding;

namespace Taqyeem.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core <see cref="TaqyeemDbContext"/> via the Aspire SQL Server integration
    /// (connection string, retries, health checks and telemetry) plus the demo data seeder.
    /// </summary>
    public static TBuilder AddInfrastructure<TBuilder>(this TBuilder builder, string connectionName = "evaluationdb")
        where TBuilder : IHostApplicationBuilder
    {
        // Acquire Azure SQL managed-identity tokens via Azure.Identity directly (see the provider's
        // remarks). No-op for the local SQL Server container, which does not use Entra auth.
        SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDefault,
            new AzureIdentitySqlAuthenticationProvider());

        builder.AddSqlServerDbContext<TaqyeemDbContext>(connectionName);
        builder.Services.AddScoped<Taqyeem.Application.Abstractions.ITaqyeemDbContext>(
            sp => sp.GetRequiredService<TaqyeemDbContext>());
        builder.Services.AddScoped<DemoDataSeeder>();
        return builder;
    }
}
