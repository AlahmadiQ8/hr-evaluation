using Taqyeem.Application.Scoring;
using Taqyeem.Domain.Common;
using Taqyeem.Domain.Evaluations;

namespace Taqyeem.Application.Tests.Scoring;

[TestClass]
public sealed class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    private static ScoredItem Item(int rating, decimal weight = 1m) =>
        new() { Name = new LocalizedText("Item", "بند"), Weight = weight, Rating = rating };

    [TestMethod]
    public void AllTopRatings_ScoreIs100_Outstanding()
    {
        ScoreResult result = _engine.Score([Item(5), Item(5)], [Item(5)]);

        Assert.AreEqual(100.0m, result.Percent);
        Assert.AreEqual(RatingBand.Outstanding, result.Band);
        Assert.AreEqual(5.00m, result.WeightedRating);
    }

    [TestMethod]
    public void AllLowestRatings_ScoreIs0_Unsatisfactory()
    {
        ScoreResult result = _engine.Score([Item(1)], [Item(1)]);

        Assert.AreEqual(0.0m, result.Percent);
        Assert.AreEqual(RatingBand.Unsatisfactory, result.Band);
    }

    [TestMethod]
    public void MidRatings_MapToExpectedBand()
    {
        // Rating 3 on the 1-5 scale => 50% => PartiallyMeets (>= 50, < 60).
        ScoreResult result = _engine.Score([Item(3)], [Item(3)]);

        Assert.AreEqual(50.0m, result.Percent);
        Assert.AreEqual(RatingBand.PartiallyMeets, result.Band);
    }

    [TestMethod]
    public void SectionWeights_AreApplied()
    {
        // Competencies=5, Objectives=1, default split 0.4/0.6 => 5*0.4 + 1*0.6 = 2.6 => 40%.
        ScoreResult result = _engine.Score([Item(5)], [Item(1)]);

        Assert.AreEqual(2.60m, result.WeightedRating);
        Assert.AreEqual(40.0m, result.Percent);
        Assert.AreEqual(RatingBand.Unsatisfactory, result.Band);
    }

    [TestMethod]
    public void ItemWeights_WithinSection_AreApplied()
    {
        // Weighted competency average: (3*5 + 1*1) / 4 = 4.0; no objectives => competency full weight.
        ScoreResult result = _engine.Score([Item(5, weight: 3m), Item(1, weight: 1m)], []);

        Assert.AreEqual(4.00m, result.WeightedRating);
        Assert.AreEqual(75.0m, result.Percent);
        Assert.AreEqual(RatingBand.Exceeds, result.Band);
    }

    [TestMethod]
    public void EmptyCompetencySection_RedistributesWeightToObjectives()
    {
        // Only objectives present (all 4s) => rating 4 => 75% => Exceeds, despite competencies being empty.
        ScoreResult result = _engine.Score([], [Item(4), Item(4)]);

        Assert.AreEqual(4.00m, result.WeightedRating);
        Assert.AreEqual(75.0m, result.Percent);
        Assert.AreEqual(RatingBand.Exceeds, result.Band);
    }

    [TestMethod]
    public void BandThreshold_IsInclusiveLowerBound_At90()
    {
        // Weighted competency average: (3*5 + 2*4) / 5 = 4.6 => exactly 90% => Outstanding.
        ScoreResult result = _engine.Score([Item(5, weight: 3m), Item(4, weight: 2m)], []);

        Assert.AreEqual(90.0m, result.Percent);
        Assert.AreEqual(RatingBand.Outstanding, result.Band);
    }

    [TestMethod]
    public void Percent_IsRoundedToOneDecimal()
    {
        // Ratings 4,4,3 equal weight => 3.6667 => 66.666...% => 66.7 => Meets.
        ScoreResult result = _engine.Score([Item(4), Item(4), Item(3)], []);

        Assert.AreEqual(66.7m, result.Percent);
        Assert.AreEqual(RatingBand.Meets, result.Band);
    }

    [TestMethod]
    public void CustomThresholds_AreHonored()
    {
        var options = new ScoringOptions
        {
            BandThresholds =
            [
                new(RatingBand.Outstanding, 50m),
                new(RatingBand.Unsatisfactory, 0m),
            ],
        };

        // 50% would normally be PartiallyMeets, but custom thresholds make >=50 Outstanding.
        ScoreResult result = _engine.Score([Item(3)], [Item(3)], options);

        Assert.AreEqual(RatingBand.Outstanding, result.Band);
    }

    [TestMethod]
    public void NoItems_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _engine.Score([], []));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(6)]
    [DataRow(-1)]
    public void RatingOutOfRange_Throws(int rating)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _engine.Score([Item(rating)], []));
    }

    [TestMethod]
    public void NegativeWeight_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _engine.Score([Item(3, weight: -1m)], []));
    }

    [TestMethod]
    public void SectionWeightsSumToZero_Throws()
    {
        // Both items have zero weight => section total weight is zero.
        Assert.ThrowsExactly<ArgumentException>(() => _engine.Score([Item(3, weight: 0m), Item(4, weight: 0m)], []));
    }
}
