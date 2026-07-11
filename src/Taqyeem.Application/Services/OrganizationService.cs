using Microsoft.EntityFrameworkCore;
using Taqyeem.Application.Abstractions;
using Taqyeem.Application.Contracts;
using Taqyeem.Domain.Organization;

namespace Taqyeem.Application.Services;

public sealed class OrganizationService(ITaqyeemDbContext db)
{
    public async Task<IReadOnlyList<OrgSectorDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        List<Sector> sectors = await db.Sectors
            .AsNoTracking()
            .Include(s => s.Departments).ThenInclude(d => d.Divisions).ThenInclude(v => v.Employees)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        return
        [
            .. sectors.Select(s => new OrgSectorDto(s.Id, s.Code, s.Name,
                [.. s.Departments.OrderBy(d => d.Code).Select(d => new OrgDepartmentDto(d.Id, d.Code, d.Name,
                    [.. d.Divisions.OrderBy(v => v.Code).Select(v => new OrgDivisionDto(v.Id, v.Code, v.Name, v.Employees.Count))]))]))
        ];
    }

    public async Task<IReadOnlyList<CycleDto>> GetCyclesAsync(CancellationToken cancellationToken = default)
    {
        return await db.EvaluationCycles
            .AsNoTracking()
            .OrderByDescending(c => c.Year)
            .Select(c => new CycleDto(c.Id, c.Name, c.Year, c.StartDate, c.EndDate, c.Status, c.Evaluations.Count))
            .ToListAsync(cancellationToken);
    }
}
