using Microsoft.EntityFrameworkCore;
using Taqyeem.Application.Abstractions;
using Taqyeem.Application.Contracts;
using Taqyeem.Application.Quota;
using Taqyeem.Domain.Evaluations;
using Taqyeem.Domain.Organization;

namespace Taqyeem.Application.Services;

/// <summary>Produces per-sector forced-distribution calibration reports for HR.</summary>
public sealed class CalibrationService(ITaqyeemDbContext db, IQuotaEngine quota)
{
    public async Task<IReadOnlyList<CalibrationDto>> GetSectorCalibrationsAsync(CancellationToken cancellationToken = default)
    {
        List<Sector> sectors = await db.Sectors.AsNoTracking().OrderBy(s => s.Code).ToListAsync(cancellationToken);

        var ratedBySector = await db.Evaluations
            .AsNoTracking()
            .Where(e => e.Band != null && e.Employee!.SectorId != null)
            .Select(e => new { SectorId = e.Employee!.SectorId!.Value, e.EmployeeId, Band = e.Band!.Value })
            .ToListAsync(cancellationToken);

        var result = new List<CalibrationDto>();
        foreach (Sector sector in sectors)
        {
            List<RatedEmployee> rated =
            [
                .. ratedBySector
                    .Where(r => r.SectorId == sector.Id)
                    .Select(r => new RatedEmployee(r.EmployeeId, r.Band))
            ];

            QuotaResult quotaResult = quota.Evaluate(rated);

            result.Add(new CalibrationDto(
                sector.Code,
                sector.Name,
                quotaResult.TotalEmployees,
                quotaResult.IsCompliant,
                [.. quotaResult.Distribution.Select(d => new BandDistributionDto(d.Band, d.Count, d.Percent, d.AllowedMax, d.IsOverQuota))],
                [.. quotaResult.Violations.Select(v => new QuotaViolationDto(v.Band, v.Actual, v.AllowedMax, v.OverBy))]));
        }

        return result;
    }
}
