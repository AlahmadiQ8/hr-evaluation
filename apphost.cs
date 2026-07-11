#:sdk Aspire.AppHost.Sdk@13.4.6+87fe259e4fc244c599019a7b1304c85a1488f248
#:package Aspire.Hosting.Azure.Sql@13.4.6
#:package Aspire.Hosting.Azure.AppContainers@13.4.6
#:package Aspire.Hosting.Azure.ApplicationInsights@13.4.6
#:package Aspire.Hosting.Azure.Network@13.4.6

var builder = DistributedApplication.CreateBuilder(args);

// The Azure virtual-network / private-endpoint hosting APIs are still marked experimental.
#pragma warning disable ASPIREAZURE003

// Build/release version stamped into the deployed apps so the release pipeline can verify the
// intended release is actually live (exposed at /version). Defaults to "dev" for local runs.
var appVersion = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";

// Azure SQL Database — runs as a local SQL Server container in dev and
// provisions Azure SQL on `aspire deploy`. Connection is injected via WithReference.
var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer(container => container.WithDataVolume());

var db = sql.AddDatabase("evaluationdb");

// Backend Web API — owns EF Core + the application rule engines. Internal-only in Azure: the
// Blazor server app calls it server-side via service discovery, so it needs no public ingress.
var api = builder.AddProject("api", "src/Taqyeem.Api/Taqyeem.Api.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("APP_VERSION", appVersion)
    .WithHttpHealthCheck("/health");

// Blazor Web App (InteractiveServer) — the bilingual UI and the only public ingress.
var web = builder.AddProject("web", "src/Taqyeem.Web/Taqyeem.Web.csproj")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("APP_VERSION", appVersion)
    .WithExternalHttpEndpoints();

// Azure-only wiring (private networking, compute environment, telemetry). Guarded to
// publish/deploy so local `aspire run` stays a pure local-container experience.
if (builder.ExecutionContext.IsPublishMode)
{
    // Private networking. The target subscription's governance denies Azure SQL public endpoints
    // (policy DenyPublicEndpointEnabled), so SQL is reached over a private endpoint and the
    // Container Apps environment runs in a delegated subnet of a dedicated virtual network.
    var vnet = builder.AddAzureVirtualNetwork("vnet", "10.0.0.0/16");
    var acaSubnet = vnet.AddSubnet("containerapps", "10.0.0.0/23");  // ACA needs a /23 delegated subnet
    var sqlSubnet = vnet.AddSubnet("sqlpe", "10.0.2.0/24");          // SQL private endpoint
    var sqlScriptSubnet = vnet.AddSubnet("sqlscript", "10.0.3.0/24"); // SQL role-assignment script

    // SQL over a private endpoint; VNet-integrate the admin (role-assignment) deployment script
    // so it can reach the private server without any public firewall rule.
    sqlSubnet.AddPrivateEndpoint(sql);
    sql.WithAdminDeploymentScriptSubnet(sqlScriptSubnet);

    // Azure Container Apps compute environment — VNet-integrated so it can reach private SQL.
    builder.AddAzureContainerAppEnvironment("aca")
        .WithDelegatedSubnet(acaSubnet);

    // Application Insights — OpenTelemetry configured in ServiceDefaults exports here.
    var insights = builder.AddAzureApplicationInsights("insights");
    api.WithReference(insights);
    web.WithReference(insights);
}

builder.Build().Run();