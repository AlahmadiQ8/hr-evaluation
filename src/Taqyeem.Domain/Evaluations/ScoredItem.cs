using Taqyeem.Domain.Common;

namespace Taqyeem.Domain.Evaluations;

/// <summary>
/// A single weighted line item on an evaluation — either a competency or an objective —
/// rated on a 1–5 scale.
/// </summary>
public sealed record ScoredItem
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public required LocalizedText Name { get; init; }

    /// <summary>Relative weight of this item within its section (competencies or objectives).</summary>
    public required decimal Weight { get; init; }

    /// <summary>Rating on the <see cref="MinRating"/>–<see cref="MaxRating"/> scale.</summary>
    public required int Rating { get; init; }
}
