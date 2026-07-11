using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Routing;

public interface IRoutingEngine
{
    IReadOnlyList<ApprovalStepDefinition> BuildApprovalChain(RoutingRequest request);

    EvaluationStage NextStage(
        IReadOnlyList<ApprovalStepDefinition> chain,
        EvaluationStage current,
        ApprovalDecision decision);

    IReadOnlyList<Approver> ManagerWeights(
        IReadOnlyList<ManagerTenure> managers,
        DateOnly cycleStart,
        DateOnly cycleEnd);
}

/// <summary>
/// Builds the ordered approval chain for an evaluation and computes stage transitions.
///
/// Normal flow climbs the management hierarchy:
/// Manager evaluation → Department review → Sector approval → HR calibration → Finalized.
///
/// Two special cases are handled:
/// <list type="bullet">
/// <item>Mid-year manager change — the manager-evaluation stage lists every line manager,
/// each weighted by the share of the cycle they managed the employee.</item>
/// <item>Managing-Director-direct reports — the intermediate department and sector approvals
/// are skipped; the MD performs the manager evaluation directly.</item>
/// </list>
/// A person who already acted at a lower level is not asked to approve again at a higher one.
/// </summary>
public sealed class RoutingEngine : IRoutingEngine
{
    public IReadOnlyList<ApprovalStepDefinition> BuildApprovalChain(RoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.LineManagers.Count == 0 && !request.ReportsDirectlyToManagingDirector)
        {
            throw new ArgumentException(
                "An evaluation must have at least one line manager, or report directly to the Managing Director.");
        }

        var steps = new List<ApprovalStepDefinition>();
        var actedApprovers = new HashSet<Guid>();
        int order = 1;

        // 1. Manager evaluation.
        IReadOnlyList<Approver> evaluators;
        if (request.ReportsDirectlyToManagingDirector)
        {
            Guid managingDirectorId = request.ManagingDirectorId
                ?? throw new ArgumentException("ManagingDirectorId is required for a Managing-Director-direct report.");
            evaluators = [new Approver(managingDirectorId, 1m)];
        }
        else
        {
            evaluators = ManagerWeights(request.LineManagers, request.CycleStart, request.CycleEnd);
        }

        steps.Add(new ApprovalStepDefinition(order++, EvaluationStage.ManagerEvaluation, evaluators));
        foreach (Approver evaluator in evaluators)
        {
            actedApprovers.Add(evaluator.EmployeeId);
        }

        // 2 & 3. Department review and sector approval — skipped for MD-direct reports.
        if (!request.ReportsDirectlyToManagingDirector)
        {
            AddSingleApproverStep(steps, ref order, actedApprovers, EvaluationStage.DepartmentReview, request.DepartmentManagerId);
            AddSingleApproverStep(steps, ref order, actedApprovers, EvaluationStage.SectorApproval, request.SectorHeadId);
        }

        // 4. HR calibration (HR always calibrates, even if it duplicates a prior approver).
        AddSingleApproverStep(steps, ref order, actedApprovers, EvaluationStage.HrCalibration, request.HrCalibratorId, allowReuse: true);

        // 5. Finalized — terminal, no approver.
        steps.Add(new ApprovalStepDefinition(order++, EvaluationStage.Finalized, []));

        return steps;
    }

    public IReadOnlyList<Approver> ManagerWeights(
        IReadOnlyList<ManagerTenure> managers,
        DateOnly cycleStart,
        DateOnly cycleEnd)
    {
        ArgumentNullException.ThrowIfNull(managers);
        if (managers.Count == 0)
        {
            throw new ArgumentException("At least one manager is required.", nameof(managers));
        }

        if (cycleEnd < cycleStart)
        {
            throw new ArgumentException("Cycle end cannot precede cycle start.", nameof(cycleEnd));
        }

        var overlaps = managers
            .Select(m =>
            {
                DateOnly start = m.Start > cycleStart ? m.Start : cycleStart;
                DateOnly end = m.End < cycleEnd ? m.End : cycleEnd;
                int days = Math.Max(0, end.DayNumber - start.DayNumber + 1);
                return (m.ManagerId, days);
            })
            .Where(o => o.days > 0)
            .ToList();

        int coveredDays = overlaps.Sum(o => o.days);
        if (coveredDays == 0)
        {
            throw new ArgumentException("Manager assignments do not overlap the cycle period.", nameof(managers));
        }

        return overlaps
            .Select(o => new Approver(o.ManagerId, Math.Round((decimal)o.days / coveredDays, 4, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public EvaluationStage NextStage(
        IReadOnlyList<ApprovalStepDefinition> chain,
        EvaluationStage current,
        ApprovalDecision decision)
    {
        ArgumentNullException.ThrowIfNull(chain);

        List<EvaluationStage> stages = [.. chain.OrderBy(s => s.Order).Select(s => s.Stage)];

        int index = stages.IndexOf(current);
        if (index < 0)
        {
            throw new ArgumentException($"Stage {current} is not part of this approval chain.", nameof(current));
        }

        if (current == EvaluationStage.Finalized)
        {
            throw new InvalidOperationException("A finalized evaluation cannot transition further.");
        }

        // A return sends the evaluation back to the manager for rework.
        return decision == ApprovalDecision.Return
            ? EvaluationStage.ManagerEvaluation
            : stages[index + 1];
    }

    private static void AddSingleApproverStep(
        List<ApprovalStepDefinition> steps,
        ref int order,
        HashSet<Guid> actedApprovers,
        EvaluationStage stage,
        Guid? approverId,
        bool allowReuse = false)
    {
        if (approverId is null)
        {
            return; // This management level does not exist for the employee — skip it.
        }

        if (!allowReuse && !actedApprovers.Add(approverId.Value))
        {
            return; // Same person already acted at a lower level — don't ask them twice.
        }

        actedApprovers.Add(approverId.Value);
        steps.Add(new ApprovalStepDefinition(order++, stage, [new Approver(approverId.Value, 1m)]));
    }
}
