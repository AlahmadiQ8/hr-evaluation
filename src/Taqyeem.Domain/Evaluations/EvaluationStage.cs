namespace Taqyeem.Domain.Evaluations;

/// <summary>
/// The stage an evaluation occupies in the approval chain. Values are ordered to match
/// the normal forward flow of a cycle.
/// </summary>
public enum EvaluationStage
{
    Draft = 0,
    ManagerEvaluation = 1,
    DepartmentReview = 2,
    SectorApproval = 3,
    HrCalibration = 4,
    Finalized = 5,
}
