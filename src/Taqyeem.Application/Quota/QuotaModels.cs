using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Quota;

/// <summary>
/// Forced-distribution policy: the maximum share of a unit's employees that may receive each
/// top band. Lower bands are uncapped.
/// </summary>
public sealed class QuotaPolicy
{
    private readonly Dictionary<RatingBand, decimal> _caps;

    public QuotaPolicy(decimal maxOutstanding = 0.10m, decimal maxExceeds = 0.25m)
    {
        if (maxOutstanding is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOutstanding), maxOutstanding, "Cap must be between 0 and 1.");
        }

        if (maxExceeds is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExceeds), maxExceeds, "Cap must be between 0 and 1.");
        }

        MaxOutstandingPercent = maxOutstanding;
        MaxExceedsPercent = maxExceeds;
        _caps = new Dictionary<RatingBand, decimal>
        {
            [RatingBand.Outstanding] = maxOutstanding,
            [RatingBand.Exceeds] = maxExceeds,
        };
    }

    public decimal MaxOutstandingPercent { get; }
    public decimal MaxExceedsPercent { get; }

    /// <summary>Capped bands mapped to their maximum share (0–1).</summary>
    public IReadOnlyDictionary<RatingBand, decimal> Caps => _caps;

    public static QuotaPolicy Default { get; } = new();
}

/// <summary>An employee's finalized (or proposed) rating within a unit being calibrated.</summary>
public sealed record RatedEmployee(Guid EmployeeId, RatingBand Band);

/// <summary>How many employees fell into a band, and whether that exceeds the band's cap.</summary>
public sealed record BandDistribution(
    RatingBand Band,
    int Count,
    decimal Percent,
    int? AllowedMax,
    bool IsOverQuota);

/// <summary>A band whose actual count exceeds its allowed maximum.</summary>
public sealed record QuotaViolation(RatingBand Band, int Actual, int AllowedMax, int OverBy);

/// <summary>The calibration outcome for a unit.</summary>
public sealed record QuotaResult(
    int TotalEmployees,
    IReadOnlyList<BandDistribution> Distribution,
    IReadOnlyList<QuotaViolation> Violations)
{
    public bool IsCompliant => Violations.Count == 0;
}
