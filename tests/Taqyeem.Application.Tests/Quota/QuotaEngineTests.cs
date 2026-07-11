using Taqyeem.Application.Quota;
using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Tests.Quota;

[TestClass]
public sealed class QuotaEngineTests
{
    private readonly QuotaEngine _engine = new();

    private static IReadOnlyList<RatedEmployee> Ratings(params (RatingBand band, int count)[] groups)
    {
        var list = new List<RatedEmployee>();
        foreach ((RatingBand band, int count) in groups)
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(new RatedEmployee(Guid.NewGuid(), band));
            }
        }

        return list;
    }

    [TestMethod]
    public void WithinCaps_IsCompliant()
    {
        // 10 employees: 1 Outstanding (cap 10% => 1), 2 Exceeds (cap 25% => 2).
        var ratings = Ratings(
            (RatingBand.Outstanding, 1),
            (RatingBand.Exceeds, 2),
            (RatingBand.Meets, 7));

        QuotaResult result = _engine.Evaluate(ratings);

        Assert.IsTrue(result.IsCompliant);
        Assert.IsEmpty(result.Violations);
        Assert.AreEqual(10, result.TotalEmployees);
    }

    [TestMethod]
    public void OutstandingOverCap_IsFlagged()
    {
        // 10 employees, 2 Outstanding but only 1 allowed.
        var ratings = Ratings(
            (RatingBand.Outstanding, 2),
            (RatingBand.Meets, 8));

        QuotaResult result = _engine.Evaluate(ratings);

        Assert.IsFalse(result.IsCompliant);
        QuotaViolation violation = result.Violations.Single();
        Assert.AreEqual(RatingBand.Outstanding, violation.Band);
        Assert.AreEqual(2, violation.Actual);
        Assert.AreEqual(1, violation.AllowedMax);
        Assert.AreEqual(1, violation.OverBy);
    }

    [TestMethod]
    public void BothTopBandsOverCap_ProduceTwoViolations()
    {
        // 20 employees: 3 Outstanding (allowed 2), 6 Exceeds (allowed 5).
        var ratings = Ratings(
            (RatingBand.Outstanding, 3),
            (RatingBand.Exceeds, 6),
            (RatingBand.Meets, 11));

        QuotaResult result = _engine.Evaluate(ratings);

        Assert.HasCount(2, result.Violations);
        Assert.IsTrue(result.Violations.Any(v => v.Band == RatingBand.Outstanding && v.OverBy == 1));
        Assert.IsTrue(result.Violations.Any(v => v.Band == RatingBand.Exceeds && v.OverBy == 1));
    }

    [TestMethod]
    public void ExactlyAtCap_IsCompliant()
    {
        // 20 employees, 2 Outstanding == floor(20 * 0.10) = 2.
        var ratings = Ratings(
            (RatingBand.Outstanding, 2),
            (RatingBand.Meets, 18));

        QuotaResult result = _engine.Evaluate(ratings);

        Assert.IsTrue(result.IsCompliant);
    }

    [TestMethod]
    public void AllowedMax_UsesFloor()
    {
        // 15 employees, cap 10% => floor(1.5) = 1 allowed; 2 Outstanding => violation.
        var ratings = Ratings(
            (RatingBand.Outstanding, 2),
            (RatingBand.Meets, 13));

        QuotaResult result = _engine.Evaluate(ratings);

        BandDistribution outstanding = result.Distribution.Single(d => d.Band == RatingBand.Outstanding);
        Assert.AreEqual(1, outstanding.AllowedMax);
        Assert.IsTrue(outstanding.IsOverQuota);
    }

    [TestMethod]
    public void EmptyUnit_IsCompliantWithZeroDistribution()
    {
        QuotaResult result = _engine.Evaluate([]);

        Assert.AreEqual(0, result.TotalEmployees);
        Assert.IsTrue(result.IsCompliant);
        Assert.IsTrue(result.Distribution.All(d => d.Count == 0));
    }

    [TestMethod]
    public void Distribution_IncludesAllBands_HighestFirst()
    {
        QuotaResult result = _engine.Evaluate(Ratings((RatingBand.Meets, 3)));

        Assert.HasCount(5, result.Distribution);
        Assert.AreEqual(RatingBand.Outstanding, result.Distribution[0].Band);
        Assert.AreEqual(RatingBand.Unsatisfactory, result.Distribution[^1].Band);
    }

    [TestMethod]
    public void Percent_IsComputedPerBand()
    {
        // 4 of 10 are Meets => 40.0%.
        var ratings = Ratings(
            (RatingBand.Meets, 4),
            (RatingBand.PartiallyMeets, 6));

        QuotaResult result = _engine.Evaluate(ratings);

        Assert.AreEqual(40.0m, result.Distribution.Single(d => d.Band == RatingBand.Meets).Percent);
    }

    [TestMethod]
    public void LowerBands_AreUncapped()
    {
        // 10 employees all Meets — uncapped band, so compliant and no allowed max.
        QuotaResult result = _engine.Evaluate(Ratings((RatingBand.Meets, 10)));

        Assert.IsTrue(result.IsCompliant);
        Assert.IsNull(result.Distribution.Single(d => d.Band == RatingBand.Meets).AllowedMax);
    }

    [TestMethod]
    public void CustomPolicy_AppliesStricterCaps()
    {
        // Zero-tolerance for Outstanding.
        var policy = new QuotaPolicy(maxOutstanding: 0m, maxExceeds: 0.25m);
        var ratings = Ratings(
            (RatingBand.Outstanding, 1),
            (RatingBand.Meets, 9));

        QuotaResult result = _engine.Evaluate(ratings, policy);

        Assert.IsFalse(result.IsCompliant);
        Assert.AreEqual(0, result.Violations.Single().AllowedMax);
    }

    [TestMethod]
    public void Policy_RejectsOutOfRangeCaps()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new QuotaPolicy(maxOutstanding: 1.5m));
    }
}
