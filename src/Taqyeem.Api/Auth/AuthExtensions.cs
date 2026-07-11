using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Taqyeem.Domain.People;

namespace Taqyeem.Api.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Configures authentication from <c>Auth:Mode</c>: "Demo" (offline persona header, the default)
    /// or "EntraId" (Microsoft Entra ID via Microsoft.Identity.Web). Role policies are the same in both.
    /// </summary>
    public static IServiceCollection AddTaqyeemAuth(this IServiceCollection services, IConfiguration configuration)
    {
        bool useEntra = string.Equals(configuration["Auth:Mode"], "EntraId", StringComparison.OrdinalIgnoreCase);

        AuthenticationBuilder authentication = services.AddAuthentication(options =>
            options.DefaultScheme = useEntra ? JwtBearerDefaults.AuthenticationScheme : DemoAuthenticationHandler.SchemeName);

        if (useEntra)
        {
            authentication.AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));
        }
        else
        {
            authentication.AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
                DemoAuthenticationHandler.SchemeName, _ => { });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy("HrOnly", policy => policy.RequireRole(nameof(Role.HrAdmin)));

        return services;
    }
}
