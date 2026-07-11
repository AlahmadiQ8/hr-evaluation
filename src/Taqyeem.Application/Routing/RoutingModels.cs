using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Routing;

/// <summary>A line manager who managed the employee for a (possibly partial) span of the cycle.</summary>
public sealed record ManagerTenure(Guid ManagerId, DateOnly Start, DateOnly End);

/// <summary>
/// Everything the <see cref="RoutingEngine"/> needs to build an approval chain for one
/// employee's evaluation.
/// </summary>
public sealed record RoutingRequest
{
    /// <summary>One entry per line manager. More than one models a mid-year manager change.</summary>
    public required IReadOnlyList<ManagerTenure> LineManagers { get; init; }

    public Guid? DepartmentManagerId { get; init; }
    public Guid? SectorHeadId { get; init; }
    public Guid? HrCalibratorId { get; init; }
    public Guid? ManagingDirectorId { get; init; }

    /// <summary>When true, intermediate department/sector approvals are skipped.</summary>
    public bool ReportsDirectlyToManagingDirector { get; init; }

    public required DateOnly CycleStart { get; init; }
    public required DateOnly CycleEnd { get; init; }
}

/// <summary>A person who acts at a stage, with a relative <paramref name="Weight"/> (used to split
/// a mid-year manager evaluation between two managers). Weights within a stage sum to 1.</summary>
public sealed record Approver(Guid EmployeeId, decimal Weight);

/// <summary>One stage of the approval chain, with its ordered position and the people who act on it.</summary>
public sealed record ApprovalStepDefinition(
    int Order,
    EvaluationStage Stage,
    IReadOnlyList<Approver> Approvers);
