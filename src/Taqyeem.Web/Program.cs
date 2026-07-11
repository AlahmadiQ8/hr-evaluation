using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Taqyeem.Web.Api;
using Taqyeem.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Arabic/English localization backed by Resources/SharedResource.*.resx.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Demo persona sign-in via cookie auth; the user flows into the Blazor circuit.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "taqyeem-auth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// API clients — base address resolved via Aspire service discovery.
IHttpClientBuilder typedApiClient = builder.Services.AddHttpClient<TaqyeemApiClient>(
    client => client.BaseAddress = new Uri("https+http://api"));
IHttpClientBuilder anonymousApiClient = builder.Services.AddHttpClient("api-anon",
    client => client.BaseAddress = new Uri("https+http://api"));

// In development (incl. CI E2E), trust the local API's self-signed dev certificate.
if (builder.Environment.IsDevelopment())
{
    typedApiClient.ConfigurePrimaryHttpMessageHandler(DevCertHandler);
    anonymousApiClient.ConfigurePrimaryHttpMessageHandler(DevCertHandler);
}

static HttpClientHandler DevCertHandler() => new()
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
};

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Bilingual (en/ar) request localization, culture chosen via the .AspNetCore.Culture cookie.
var supportedCultures = new[] { "en", "ar" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Sets the culture cookie then returns to the originating page (used by the language toggle).
app.MapGet("/set-culture", (string culture, string redirectUri, HttpContext http) =>
{
    if (!string.IsNullOrWhiteSpace(culture))
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Path = "/", Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
    }

    return Results.LocalRedirect(string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri);
});

// Demo login: sign in as the selected persona (fetched from the API), then return to the app.
app.MapGet("/demo/login", async (Guid personaId, HttpContext http, IHttpClientFactory factory, CancellationToken ct) =>
{
    HttpClient client = factory.CreateClient("api-anon");
    List<PersonaDto> personas = await client.GetFromJsonAsync<List<PersonaDto>>("/api/personas", ct) ?? [];
    PersonaDto? persona = personas.FirstOrDefault(p => p.Id == personaId);
    if (persona is null)
    {
        return Results.LocalRedirect("/login");
    }

    var identity = new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, persona.Id.ToString()),
        new Claim(ClaimTypes.Name, persona.Name.En),
        new Claim("name_ar", persona.Name.Ar),
        new Claim(ClaimTypes.Role, persona.Role),
        new Claim("employee_number", persona.EmployeeNumber),
    ], CookieAuthenticationDefaults.AuthenticationScheme);

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.LocalRedirect("/");
});

app.MapGet("/demo/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
