using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace Taqyeem.Infrastructure.Persistence;

/// <summary>
/// Supplies access tokens for <c>Authentication=Active Directory Default</c> Azure SQL connections
/// using <see cref="DefaultAzureCredential"/> directly.
/// </summary>
/// <remarks>
/// This replaces the provider from <c>Microsoft.Data.SqlClient.Extensions.Azure</c>, whose
/// reflection-based assembly loading fails in trimmed container publishes
/// (<c>FileNotFoundException: Could not load ... 'Azure.Identity'</c>). Because <c>Azure.Identity</c>
/// is referenced statically here, it is always included in the published output. Only exercised
/// against Azure SQL; the local SQL Server container uses a normal connection string.
/// </remarks>
internal sealed class AzureIdentitySqlAuthenticationProvider : SqlAuthenticationProvider
{
    private static readonly TokenCredential Credential = new DefaultAzureCredential();

    public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        => authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDefault;

    public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
    {
        var resource = parameters.Resource.TrimEnd('/');
        var scope = resource.EndsWith("/.default", StringComparison.Ordinal) ? resource : resource + "/.default";

        AccessToken token = await Credential.GetTokenAsync(
            new TokenRequestContext([scope]),
            CancellationToken.None).ConfigureAwait(false);

        return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
    }
}
