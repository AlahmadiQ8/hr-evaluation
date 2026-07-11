using Taqyeem.Application;
using Taqyeem.Infrastructure;
using Taqyeem.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Business-rule engines + EF Core (via the Aspire SQL Server integration).
builder.Services.AddApplication();
builder.AddInfrastructure();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations and seed the fictional demo data (idempotent).
if (builder.Configuration.GetValue("DemoData:Seed", true))
{
    using IServiceScope scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "Taqyeem API");

app.Run();
