using Taqyeem.Domain.Common;

namespace Taqyeem.Domain.Evaluations;

/// <summary>An annual appraisal round.</summary>
public class EvaluationCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required LocalizedText Name { get; set; }
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public CycleStatus Status { get; set; } = CycleStatus.Draft;

    public List<Evaluation> Evaluations { get; } = [];
}
