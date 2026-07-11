using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taqyeem.Application.Abstractions;

namespace Taqyeem.Api.Auth;

/// <summary>
/// Offline "demo" authentication: the caller selects a seeded persona by sending its employee id in
/// the <c>X-Demo-Persona</c> header. No Entra tenant is required, so the demo runs fully offline.
/// </summary>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITaqyeemDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";
    public const string PersonaHeader = "X-Demo-Persona";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(PersonaHeader, out var values) || string.IsNullOrWhiteSpace(values))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Guid.TryParse(values.ToString(), out Guid personaId))
        {
            return AuthenticateResult.Fail("Invalid persona identifier.");
        }

        var employee = await db.Employees
            .AsNoTracking()
            .Where(e => e.Id == personaId)
            .Select(e => new { e.Id, NameEn = e.Name.En, e.Role, e.EmployeeNumber })
            .FirstOrDefaultAsync();

        if (employee is null)
        {
            return AuthenticateResult.Fail("Unknown persona.");
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new(ClaimTypes.Name, employee.NameEn),
            new(ClaimTypes.Role, employee.Role.ToString()),
            new("employee_number", employee.EmployeeNumber),
        ];

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
