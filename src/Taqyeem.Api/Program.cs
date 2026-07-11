using System.Text.Json.Serialization;
using Taqyeem.Api;
using Taqyeem.Api.Auth;
using Taqyeem.Api.Endpoints;
using Taqyeem.Application;
using Taqyeem.Infrastructure;
using Taqyeem.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Business-rule engines + EF Core (via the Aspire SQL Server integration).
builder.Services.AddApplication();
builder.AddInfrastructure();

// Authentication (Demo persona header or Entra ID) + the current-user accessor.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Taqyeem.Application.Abstractions.ICurrentUser, CurrentUser>();
builder.Services.AddTaqyeemAuth(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations and seed the fictional demo data (idempotent).
if (builder.Configuration.GetValue("DemoData:Seed", true))
{
    using IServiceScope scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Taqyeem API");
app.MapTaqyeemEndpoints();

app.Run();
