using Taqyeem.Domain.People;

namespace Taqyeem.Domain.Evaluations;

/// <summary>One employee's appraisal within a cycle, moving through the approval chain.</summary>
public class Evaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CycleId { get; set; }
    public EvaluationCycle? Cycle { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public EvaluationStage Stage { get; set; } = EvaluationStage.Draft;

    // Populated once scored.
    public decimal? ScorePercent { get; set; }
    public decimal? WeightedRating { get; set; }
    public RatingBand? Band { get; set; }

    public List<EvaluationItem> Items { get; } = [];
    public List<ApprovalStep> ApprovalSteps { get; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
