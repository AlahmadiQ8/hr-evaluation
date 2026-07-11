using Microsoft.EntityFrameworkCore;
using Taqyeem.Application.Abstractions;
using Taqyeem.Application.Contracts;
using Taqyeem.Domain.People;

namespace Taqyeem.Application.Services;

/// <summary>Provides the curated set of demo login personas (one per role + the special cases).</summary>
public sealed class PersonaService(ITaqyeemDbContext db)
{
    // Ordered for the login screen: executives first, then the special cases.
    private static readonly string[] PersonaNumbers =
    [
        "MD-001", "HR-001", "SEC-INV", "DEP-INV-EQ", "LM-INV-EQ-LOCAL", "EMP-000", "EMP-MIDYEAR", "EMP-CHIEF",
    ];

    public async Task<IReadOnlyList<PersonaDto>> GetPersonasAsync(CancellationToken cancellationToken = default)
    {
        List<Employee> employees = await db.Employees
            .AsNoTracking()
            .Where(e => PersonaNumbers.Contains(e.EmployeeNumber))
            .ToListAsync(cancellationToken);

        return [.. employees
            .OrderBy(e => Array.IndexOf(PersonaNumbers, e.EmployeeNumber))
            .Select(e => new PersonaDto(e.Id, e.EmployeeNumber, e.Name, e.JobTitle, e.Role))];
    }
}
