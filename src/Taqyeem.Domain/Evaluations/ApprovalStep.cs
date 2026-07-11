using Taqyeem.Domain.People;

namespace Taqyeem.Domain.Evaluations;

/// <summary>A single step in an evaluation's approval chain.</summary>
public class ApprovalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EvaluationId { get; set; }
    public Evaluation? Evaluation { get; set; }

    public int Order { get; set; }
    public EvaluationStage Stage { get; set; }

    public Guid? ApproverId { get; set; }
    public Employee? Approver { get; set; }

    /// <summary>Relative weight when a stage has multiple approvers (mid-year manager split).</summary>
    public decimal Weight { get; set; } = 1m;

    public ApprovalDecision? Decision { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
