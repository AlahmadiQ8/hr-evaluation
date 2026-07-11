using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Scoring;

public interface IScoringEngine
{
    ScoreResult Score(
        IReadOnlyList<ScoredItem> competencies,
        IReadOnlyList<ScoredItem> objectives,
        ScoringOptions? options = null);
}

/// <summary>
/// Computes an evaluation's overall score and rating band from weighted competency and
/// objective line items.
///
/// Each section (competencies, objectives) is reduced to a weighted-average 1–5 rating.
/// The two section ratings are combined using the configured section weights, converted to
/// a 0–100 percentage, and mapped to a <see cref="RatingBand"/>. When a section has no
/// items, its weight is redistributed to the other section.
/// </summary>
public sealed class ScoringEngine : IScoringEngine
{
    public ScoreResult Score(
        IReadOnlyList<ScoredItem> competencies,
        IReadOnlyList<ScoredItem> objectives,
        ScoringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(competencies);
        ArgumentNullException.ThrowIfNull(objectives);
        options ??= new ScoringOptions();

        ValidateItems(competencies, nameof(competencies));
        ValidateItems(objectives, nameof(objectives));

        if (competencies.Count == 0 && objectives.Count == 0)
        {
            throw new ArgumentException("At least one competency or objective is required to score an evaluation.");
        }

        decimal? competencyRating = SectionAverage(competencies, nameof(competencies));
        decimal? objectiveRating = SectionAverage(objectives, nameof(objectives));

        (decimal competencyWeight, decimal objectiveWeight) =
            EffectiveWeights(options, competencyRating.HasValue, objectiveRating.HasValue);

        decimal weightedRating =
            (competencyRating ?? 0m) * competencyWeight +
            (objectiveRating ?? 0m) * objectiveWeight;

        decimal percent = ToPercent(weightedRating);
        RatingBand band = BandFor(percent, options.BandThresholds);

        return new ScoreResult(
            Percent: Math.Round(percent, 1, MidpointRounding.AwayFromZero),
            Band: band,
            WeightedRating: Math.Round(weightedRating, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal? SectionAverage(IReadOnlyList<ScoredItem> items, string paramName)
    {
        if (items.Count == 0)
        {
            return null;
        }

        decimal totalWeight = items.Sum(i => i.Weight);
        if (totalWeight <= 0m)
        {
            throw new ArgumentException("Section item weights must sum to a positive value.", paramName);
        }

        decimal weighted = items.Sum(i => i.Weight * i.Rating);
        return weighted / totalWeight;
    }

    private static (decimal competency, decimal objective) EffectiveWeights(
        ScoringOptions options, bool hasCompetencies, bool hasObjectives)
    {
        decimal competency = hasCompetencies ? options.CompetencyWeight : 0m;
        decimal objective = hasObjectives ? options.ObjectiveWeight : 0m;
        decimal sum = competency + objective;

        if (sum <= 0m)
        {
            throw new ArgumentException("Scoring options must define positive section weights.", nameof(options));
        }

        // Normalize so the effective section weights always sum to 1.
        return (competency / sum, objective / sum);
    }

    private static decimal ToPercent(decimal rating) =>
        (rating - ScoredItem.MinRating) / (ScoredItem.MaxRating - ScoredItem.MinRating) * 100m;

    private static RatingBand BandFor(decimal percent, IReadOnlyList<BandThreshold> thresholds)
    {
        foreach (BandThreshold threshold in thresholds.OrderByDescending(t => t.MinPercent))
        {
            if (percent >= threshold.MinPercent)
            {
                return threshold.Band;
            }
        }

        return RatingBand.Unsatisfactory;
    }

    private static void ValidateItems(IReadOnlyList<ScoredItem> items, string paramName)
    {
        foreach (ScoredItem item in items)
        {
            if (item.Rating is < ScoredItem.MinRating or > ScoredItem.MaxRating)
            {
                throw new ArgumentOutOfRangeException(
                    paramName, item.Rating,
                    $"Rating must be between {ScoredItem.MinRating} and {ScoredItem.MaxRating}.");
            }

            if (item.Weight < 0m)
            {
                throw new ArgumentException("Item weight cannot be negative.", paramName);
            }
        }
    }
}
