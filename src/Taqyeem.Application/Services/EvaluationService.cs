using Microsoft.EntityFrameworkCore;
using Taqyeem.Application.Abstractions;
using Taqyeem.Application.Contracts;
using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Services;

public sealed class EvaluationService(ITaqyeemDbContext db)
{
    /// <summary>Evaluations currently awaiting the given user's action at their pending stage.</summary>
    public async Task<IReadOnlyList<EvaluationSummaryDto>> GetInboxAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        List<Evaluation> evaluations = await db.Evaluations
            .AsNoTracking()
            .Include(e => e.Employee).ThenInclude(emp => emp!.Division)
            .Where(e => e.ApprovalSteps.Any(s => s.Stage == e.Stage && s.ApproverId == userId && s.Decision == null))
            .OrderBy(e => e.Stage)
            .ToListAsync(cancellationToken);

        return [.. evaluations.Select(ToSummary)];
    }

    public async Task<IReadOnlyList<EvaluationSummaryDto>> GetByCycleAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        List<Evaluation> evaluations = await db.Evaluations
            .AsNoTracking()
            .Include(e => e.Employee).ThenInclude(emp => emp!.Division)
            .Where(e => e.CycleId == cycleId)
            .OrderBy(e => e.Employee!.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return [.. evaluations.Select(ToSummary)];
    }

    public async Task<EvaluationDetailDto?> GetDetailAsync(Guid id, Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        Evaluation? e = await db.Evaluations
            .AsNoTracking()
            .Include(x => x.Employee).ThenInclude(emp => emp!.Division)
            .Include(x => x.Items)
            .Include(x => x.ApprovalSteps).ThenInclude(s => s.Approver)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (e is null)
        {
            return null;
        }

        bool canAct = currentUserId is not null
            && e.ApprovalSteps.Any(s => s.Stage == e.Stage && s.ApproverId == currentUserId && s.Decision is null);

        return new EvaluationDetailDto(
            e.Id,
            e.EmployeeId,
            e.Employee!.Name,
            e.Employee.JobTitle,
            e.Employee.Division?.Name,
            e.Stage,
            e.ScorePercent,
            e.WeightedRating,
            e.Band,
            [.. e.Items.OrderBy(i => i.Kind).Select(i => new EvaluationItemDto(i.Id, i.Kind, i.Name, i.Weight, i.Rating))],
            [.. e.ApprovalSteps.OrderBy(s => s.Order).Select(s => new ApprovalStepDto(
                s.Order, s.Stage, s.ApproverId, s.Approver?.Name, s.Weight, s.Decision, s.Comment, s.DecidedAt))],
            canAct);
    }

    private static EvaluationSummaryDto ToSummary(Evaluation e) => new(
        e.Id,
        e.EmployeeId,
        e.Employee!.Name,
        e.Employee.JobTitle,
        e.Employee.Division?.Name,
        e.Stage,
        e.ScorePercent,
        e.Band);
}
