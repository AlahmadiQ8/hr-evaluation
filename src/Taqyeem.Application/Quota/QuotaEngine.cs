using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Quota;

public interface IQuotaEngine
{
    QuotaResult Evaluate(IReadOnlyCollection<RatedEmployee> ratings, QuotaPolicy? policy = null);
}

/// <summary>
/// Applies a forced-distribution <see cref="QuotaPolicy"/> to a unit's ratings: it computes the
/// band distribution and flags bands whose count exceeds the allowed maximum. The allowed maximum
/// for a capped band is <c>floor(total × cap)</c>.
/// </summary>
public sealed class QuotaEngine : IQuotaEngine
{
    public QuotaResult Evaluate(IReadOnlyCollection<RatedEmployee> ratings, QuotaPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        policy ??= QuotaPolicy.Default;

        int total = ratings.Count;
        Dictionary<RatingBand, int> countsByBand = ratings
            .GroupBy(r => r.Band)
            .ToDictionary(g => g.Key, g => g.Count());

        var distribution = new List<BandDistribution>();
        var violations = new List<QuotaViolation>();

        // Highest band first, for readable reports.
        foreach (RatingBand band in Enum.GetValues<RatingBand>().OrderByDescending(b => b))
        {
            int count = countsByBand.GetValueOrDefault(band, 0);
            decimal percent = total == 0
                ? 0m
                : Math.Round((decimal)count / total * 100m, 1, MidpointRounding.AwayFromZero);

            int? allowedMax = null;
            bool isOverQuota = false;

            if (policy.Caps.TryGetValue(band, out decimal cap))
            {
                allowedMax = (int)Math.Floor(total * cap);
                if (count > allowedMax)
                {
                    isOverQuota = true;
                    violations.Add(new QuotaViolation(band, count, allowedMax.Value, count - allowedMax.Value));
                }
            }

            distribution.Add(new BandDistribution(band, count, percent, allowedMax, isOverQuota));
        }

        return new QuotaResult(total, distribution, violations);
    }
}
