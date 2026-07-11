using Taqyeem.Domain.Common;
using Taqyeem.Domain.Evaluations;
using Taqyeem.Domain.People;

namespace Taqyeem.Application.Contracts;

// ----- Personas & organization -----

public sealed record PersonaDto(
    Guid Id,
    string EmployeeNumber,
    LocalizedText Name,
    LocalizedText JobTitle,
    Role Role);

public sealed record OrgSectorDto(Guid Id, string Code, LocalizedText Name, IReadOnlyList<OrgDepartmentDto> Departments);
public sealed record OrgDepartmentDto(Guid Id, string Code, LocalizedText Name, IReadOnlyList<OrgDivisionDto> Divisions);
public sealed record OrgDivisionDto(Guid Id, string Code, LocalizedText Name, int EmployeeCount);

// ----- Cycles -----

public sealed record CycleDto(
    Guid Id,
    LocalizedText Name,
    int Year,
    DateOnly StartDate,
    DateOnly EndDate,
    CycleStatus Status,
    int EvaluationCount);

// ----- Evaluations -----

public sealed record EvaluationSummaryDto(
    Guid Id,
    Guid EmployeeId,
    LocalizedText EmployeeName,
    LocalizedText JobTitle,
    LocalizedText? DivisionName,
    EvaluationStage Stage,
    decimal? ScorePercent,
    RatingBand? Band);

public sealed record EvaluationItemDto(
    Guid Id,
    EvaluationItemKind Kind,
    LocalizedText Name,
    decimal Weight,
    int? Rating);

public sealed record ApprovalStepDto(
    int Order,
    EvaluationStage Stage,
    Guid? ApproverId,
    LocalizedText? ApproverName,
    decimal Weight,
    ApprovalDecision? Decision,
    string? Comment,
    DateTimeOffset? DecidedAt);

public sealed record EvaluationDetailDto(
    Guid Id,
    Guid EmployeeId,
    LocalizedText EmployeeName,
    LocalizedText JobTitle,
    LocalizedText? DivisionName,
    EvaluationStage Stage,
    decimal? ScorePercent,
    decimal? WeightedRating,
    RatingBand? Band,
    IReadOnlyList<EvaluationItemDto> Items,
    IReadOnlyList<ApprovalStepDto> Steps,
    bool CanCurrentUserAct);

// ----- Calibration -----

public sealed record BandDistributionDto(RatingBand Band, int Count, decimal Percent, int? AllowedMax, bool IsOverQuota);
public sealed record QuotaViolationDto(RatingBand Band, int Actual, int AllowedMax, int OverBy);

public sealed record CalibrationDto(
    string SectorCode,
    LocalizedText SectorName,
    int Total,
    bool IsCompliant,
    IReadOnlyList<BandDistributionDto> Distribution,
    IReadOnlyList<QuotaViolationDto> Violations);

// ----- Action requests -----

public sealed record ItemRatingInput(Guid ItemId, int Rating);
public sealed record ManagerSubmitRequest(IReadOnlyList<ItemRatingInput> Items, string? Comment);
public sealed record DecisionRequest(ApprovalDecision Decision, string? Comment);
