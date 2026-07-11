namespace Taqyeem.Domain.Evaluations;

/// <summary>
/// Final performance rating band. Numeric values are ordered from lowest to highest
/// so bands can be compared and sorted directly.
/// </summary>
public enum RatingBand
{
    Unsatisfactory = 1,
    PartiallyMeets = 2,
    Meets = 3,
    Exceeds = 4,
    Outstanding = 5,
}
