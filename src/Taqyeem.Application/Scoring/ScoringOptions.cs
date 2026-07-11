using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Scoring;

/// <summary>Inclusive lower bound (percent, 0–100) at which an evaluation earns <paramref name="Band"/>.</summary>
public readonly record struct BandThreshold(RatingBand Band, decimal MinPercent);

/// <summary>
/// Tunable scoring configuration: how competencies vs. objectives are weighted, and the
/// percentage thresholds that map an overall score to a <see cref="RatingBand"/>.
/// </summary>
public sealed class ScoringOptions
{
    /// <summary>Portion of the overall score contributed by competencies (0–1).</summary>
    public decimal CompetencyWeight { get; init; } = 0.40m;

    /// <summary>Portion of the overall score contributed by objectives (0–1).</summary>
    public decimal ObjectiveWeight { get; init; } = 0.60m;

    /// <summary>Band thresholds as inclusive lower bounds on the 0–100 score.</summary>
    public IReadOnlyList<BandThreshold> BandThresholds { get; init; } = DefaultThresholds;

    public static readonly IReadOnlyList<BandThreshold> DefaultThresholds =
    [
        new(RatingBand.Outstanding, 90m),
        new(RatingBand.Exceeds, 75m),
        new(RatingBand.Meets, 60m),
        new(RatingBand.PartiallyMeets, 50m),
        new(RatingBand.Unsatisfactory, 0m),
    ];
}
