#:sdk Aspire.AppHost.Sdk@13.4.6+87fe259e4fc244c599019a7b1304c85a1488f248
#:package Aspire.Hosting.Azure.Sql@13.4.6

var builder = DistributedApplication.CreateBuilder(args);

// Azure SQL Database — runs as a local SQL Server container in dev and
// provisions Azure SQL on `aspire deploy`. Connection is injected via WithReference.
var sql = builder.AddAzureSqlServer("sql")
    .RunAsContainer(container => container.WithDataVolume());

var db = sql.AddDatabase("evaluationdb");

// Backend Web API — owns EF Core + the application rule engines.
var api = builder.AddProject("api", "src/Taqyeem.Api/Taqyeem.Api.csproj")
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// Blazor Web App (InteractiveServer) — the bilingual UI, calls the API via service discovery.
builder.AddProject("web", "src/Taqyeem.Web/Taqyeem.Web.csproj")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();