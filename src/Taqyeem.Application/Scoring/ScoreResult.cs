using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Scoring;

/// <summary>The outcome of scoring an evaluation.</summary>
/// <param name="Percent">Overall score on a 0–100 scale, rounded to one decimal.</param>
/// <param name="Band">The rating band the score falls into.</param>
/// <param name="WeightedRating">The underlying weighted rating on the 1–5 scale.</param>
public sealed record ScoreResult(decimal Percent, RatingBand Band, decimal WeightedRating);
