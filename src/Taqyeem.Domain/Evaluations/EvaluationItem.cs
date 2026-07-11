using Taqyeem.Domain.Common;

namespace Taqyeem.Domain.Evaluations;

/// <summary>A single competency or objective line on an evaluation.</summary>
public class EvaluationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EvaluationId { get; set; }
    public Evaluation? Evaluation { get; set; }

    public EvaluationItemKind Kind { get; set; }
    public required LocalizedText Name { get; set; }
    public decimal Weight { get; set; }

    /// <summary>Rating on the 1–5 scale; null until the manager scores it.</summary>
    public int? Rating { get; set; }
}
