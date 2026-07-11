namespace Taqyeem.Web.Api;

/// <summary>Bilingual text as returned by the API.</summary>
public record LocalizedText(string En, string Ar)
{
    public string For(string twoLetterIsoLanguageName) =>
        string.Equals(twoLetterIsoLanguageName, "ar", StringComparison.OrdinalIgnoreCase) ? Ar : En;
}

public record PersonaDto(Guid Id, string EmployeeNumber, LocalizedText Name, LocalizedText JobTitle, string Role);

public record OrgSectorDto(Guid Id, string Code, LocalizedText Name, List<OrgDepartmentDto> Departments);
public record OrgDepartmentDto(Guid Id, string Code, LocalizedText Name, List<OrgDivisionDto> Divisions);
public record OrgDivisionDto(Guid Id, string Code, LocalizedText Name, int EmployeeCount);

public record CycleDto(Guid Id, LocalizedText Name, int Year, DateOnly StartDate, DateOnly EndDate, string Status, int EvaluationCount);

public record EvaluationSummaryDto(
    Guid Id,
    Guid EmployeeId,
    LocalizedText EmployeeName,
    LocalizedText JobTitle,
    LocalizedText? DivisionName,
    string Stage,
    decimal? ScorePercent,
    string? Band);

public record EvaluationItemDto(Guid Id, string Kind, LocalizedText Name, decimal Weight, int? Rating);

public record ApprovalStepDto(
    int Order,
    string Stage,
    Guid? ApproverId,
    LocalizedText? ApproverName,
    decimal Weight,
    string? Decision,
    string? Comment,
    DateTimeOffset? DecidedAt);

public record EvaluationDetailDto(
    Guid Id,
    Guid EmployeeId,
    LocalizedText EmployeeName,
    LocalizedText JobTitle,
    LocalizedText? DivisionName,
    string Stage,
    decimal? ScorePercent,
    decimal? WeightedRating,
    string? Band,
    List<EvaluationItemDto> Items,
    List<ApprovalStepDto> Steps,
    bool CanCurrentUserAct);

public record BandDistributionDto(string Band, int Count, decimal Percent, int? AllowedMax, bool IsOverQuota);
public record QuotaViolationDto(string Band, int Actual, int AllowedMax, int OverBy);
public record CalibrationDto(
    string SectorCode,
    LocalizedText SectorName,
    int Total,
    bool IsCompliant,
    List<BandDistributionDto> Distribution,
    List<QuotaViolationDto> Violations);

public record ItemRatingInput(Guid ItemId, int Rating);
public record ManagerSubmitRequest(List<ItemRatingInput> Items, string? Comment);
public record DecisionRequest(string Decision, string? Comment);
