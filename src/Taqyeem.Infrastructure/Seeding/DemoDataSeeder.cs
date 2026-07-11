using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Taqyeem.Application.Routing;
using Taqyeem.Application.Scoring;
using Taqyeem.Domain.Common;
using Taqyeem.Domain.Evaluations;
using Taqyeem.Domain.Organization;
using Taqyeem.Domain.People;
using Taqyeem.Infrastructure.Persistence;

namespace Taqyeem.Infrastructure.Seeding;

/// <summary>
/// Applies migrations and, if the database is empty, seeds a full fictional KIA-like organization:
/// sectors/departments/divisions, employees with bilingual names, manager assignments (including a
/// mid-year manager change and a Managing-Director-direct report), one active mid-flight cycle, and
/// evaluations spread across every approval stage — including a deliberate quota violation for the
/// HR calibration screen. ALL DATA IS FICTIONAL.
/// </summary>
public sealed class DemoDataSeeder(
    TaqyeemDbContext db,
    IScoringEngine scoring,
    IRoutingEngine routing,
    ILogger<DemoDataSeeder> logger)
{
    private static readonly DateOnly CycleStart = new(2025, 1, 1);
    private static readonly DateOnly CycleEnd = new(2025, 12, 31);
    private static readonly DateOnly MidYear = new(2025, 8, 31);

    private readonly Dictionary<string, Employee> _people = [];
    private readonly Dictionary<Guid, List<ManagerAssignment>> _assignmentsByEmployee = [];
    private EvaluationCycle _cycle = null!;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Employees.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already seeded; skipping.");
            return;
        }

        logger.LogInformation("Seeding fictional demo data...");

        BuildOrganization(out var divisions);
        BuildPeople(divisions);
        BuildManagerAssignments(divisions);
        BuildCycle();
        BuildEvaluations();

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seed complete: {Employees} employees, {Evaluations} evaluations.",
            _people.Count, _cycle.Evaluations.Count);
    }

    private static LocalizedText L(string en, string ar) => new(en, ar);

    // ----- Organization -------------------------------------------------------------------

    private void BuildOrganization(out Dictionary<string, Division> divisions)
    {
        divisions = [];

        (string code, LocalizedText name, (string code, LocalizedText name, (string code, LocalizedText name)[] divs)[] depts)[] tree =
        [
            ("INV", L("Investment", "الاستثمار"),
            [
                ("INV-EQ", L("Equities", "الأسهم"),
                [
                    ("INV-EQ-LOCAL", L("Local Equities", "الأسهم المحلية")),
                    ("INV-EQ-INTL", L("International Equities", "الأسهم الدولية")),
                ]),
                ("INV-FI", L("Fixed Income", "الدخل الثابت"),
                [
                    ("INV-FI-GOV", L("Government Bonds", "السندات الحكومية")),
                    ("INV-FI-CORP", L("Corporate Credit", "الائتمان المؤسسي")),
                ]),
            ]),
            ("OPS", L("Operations & Support", "العمليات والدعم"),
            [
                ("OPS-IT", L("Information Technology", "تقنية المعلومات"),
                [
                    ("OPS-IT-INFRA", L("Infrastructure", "البنية التحتية")),
                    ("OPS-IT-APPS", L("Applications", "التطبيقات")),
                ]),
                ("OPS-HR", L("Human Resources", "الموارد البشرية"),
                [
                    ("OPS-HR-TAL", L("Talent", "المواهب")),
                    ("OPS-HR-OPS", L("HR Operations", "عمليات الموارد البشرية")),
                ]),
            ]),
        ];

        foreach (var (sectorCode, sectorName, depts) in tree)
        {
            var sector = new Sector { Code = sectorCode, Name = sectorName };
            db.Sectors.Add(sector);
            _sectorIdByCode[sectorCode] = sector.Id;

            foreach (var (deptCode, deptName, divs) in depts)
            {
                var department = new Department { Code = deptCode, Name = deptName, SectorId = sector.Id };
                sector.Departments.Add(department);
                _departmentIdByCode[deptCode] = department.Id;

                foreach (var (divCode, divName) in divs)
                {
                    var division = new Division { Code = divCode, Name = divName, DepartmentId = department.Id };
                    department.Divisions.Add(division);
                    divisions[divCode] = division;
                }
            }
        }
    }

    // ----- People -------------------------------------------------------------------------

    private void BuildPeople(Dictionary<string, Division> divisions)
    {
        // Executive.
        Add("MD-001", L("Ahmad Al-Rashid", "أحمد الرشيد"), L("Managing Director", "العضو المنتدب"),
            Role.ManagingDirector, grade: 20);
        Add("HR-001", L("Layla Al-Sabah", "ليلى الصباح"), L("HR Administrator", "مسؤولة الموارد البشرية"),
            Role.HrAdmin, grade: 15, sector: "OPS", department: "OPS-HR");

        // Sector heads.
        Add("SEC-INV", L("Fatima Al-Otaibi", "فاطمة العتيبي"), L("Head of Investment", "رئيسة قطاع الاستثمار"),
            Role.SectorHead, grade: 18, sector: "INV");
        Add("SEC-OPS", L("Mishari Al-Enezi", "مشاري العنزي"), L("Head of Operations", "رئيس قطاع العمليات"),
            Role.SectorHead, grade: 18, sector: "OPS");

        // Department managers.
        Add("DEP-INV-EQ", L("Yousef Al-Kandari", "يوسف الكندري"), L("Equities Manager", "مدير الأسهم"),
            Role.DepartmentManager, grade: 16, sector: "INV", department: "INV-EQ");
        Add("DEP-INV-FI", L("Dana Al-Sabah", "دانة الصباح"), L("Fixed Income Manager", "مديرة الدخل الثابت"),
            Role.DepartmentManager, grade: 16, sector: "INV", department: "INV-FI");
        Add("DEP-OPS-IT", L("Tariq Al-Mansour", "طارق المنصور"), L("IT Manager", "مدير تقنية المعلومات"),
            Role.DepartmentManager, grade: 16, sector: "OPS", department: "OPS-IT");
        Add("DEP-OPS-HR", L("Hessa Al-Ali", "حصة العلي"), L("HR Manager", "مديرة الموارد البشرية"),
            Role.DepartmentManager, grade: 16, sector: "OPS", department: "OPS-HR");

        // Division heads (line managers) — one per division.
        var lineManagerNames = new Dictionary<string, LocalizedText>
        {
            ["INV-EQ-LOCAL"] = L("Noura Al-Mutairi", "نورة المطيري"),
            ["INV-EQ-INTL"] = L("Bader Al-Otaibi", "بدر العتيبي"),
            ["INV-FI-GOV"] = L("Salem Al-Azmi", "سالم العازمي"),
            ["INV-FI-CORP"] = L("Maryam Al-Hajri", "مريم الهاجري"),
            ["OPS-IT-INFRA"] = L("Faisal Al-Dosari", "فيصل الدوسري"),
            ["OPS-IT-APPS"] = L("Reem Al-Shammari", "ريم الشمري"),
            ["OPS-HR-TAL"] = L("Abdullah Al-Rashidi", "عبدالله الرشيدي"),
            ["OPS-HR-OPS"] = L("Ghada Al-Failakawi", "غادة الفيلكاوي"),
        };

        foreach (var (divCode, name) in lineManagerNames)
        {
            Division division = divisions[divCode];
            Add($"LM-{divCode}", name, L("Division Head", "رئيس القسم"),
                Role.LineManager, grade: 14,
                sector: SectorCodeOf(divCode), department: DepartmentCodeOf(divCode), division: division);
        }

        // Rank-and-file — two per division.
        var firstNames = new (string en, string ar)[]
        {
            ("Khaled", "خالد"), ("Sara", "سارة"), ("Omar", "عمر"), ("Aisha", "عائشة"),
            ("Fahad", "فهد"), ("Latifa", "لطيفة"), ("Nasser", "ناصر"), ("Huda", "هدى"),
            ("Waleed", "وليد"), ("Muna", "منى"), ("Saad", "سعد"), ("Amal", "أمل"),
            ("Jassim", "جاسم"), ("Wafa", "وفاء"), ("Rakan", "راكان"), ("Dalal", "دلال"),
        };
        var lastNames = new (string en, string ar)[]
        {
            ("Al-Ajmi", "العجمي"), ("Al-Fahad", "الفهد"), ("Al-Harbi", "الحربي"), ("Al-Qattan", "القطان"),
        };

        int i = 0;
        foreach (var divCode in divisions.Keys)
        {
            Division division = divisions[divCode];
            for (int n = 0; n < 2; n++)
            {
                (string en, string ar) first = firstNames[i % firstNames.Length];
                (string en, string ar) last = lastNames[(i / firstNames.Length) % lastNames.Length];
                Add($"EMP-{i:000}",
                    L($"{first.en} {last.en}", $"{first.ar} {last.ar}"),
                    L("Analyst", "محلل"),
                    Role.Employee, grade: 8 + (i % 4),
                    sector: SectorCodeOf(divCode), department: DepartmentCodeOf(divCode), division: division);
                i++;
            }
        }

        // Special case 1 — a mid-year manager change (managed by two division heads across the year).
        Add("EMP-MIDYEAR", L("Sara Al-Fahad", "سارة الفهد"), L("Senior Analyst", "محلل أول"),
            Role.Employee, grade: 11,
            sector: "INV", department: "INV-EQ", division: divisions["INV-EQ-LOCAL"]);

        // Special case 2 — a Managing-Director-direct report.
        Employee omar = Add("EMP-CHIEF", L("Omar Al-Sabah", "عمر الصباح"), L("Chief of Staff", "رئيس الديوان"),
            Role.Employee, grade: 16, sector: "INV");
        omar.ReportsDirectlyToManagingDirector = true;
    }

    private Employee Add(
        string number, LocalizedText name, LocalizedText jobTitle, Role role, int grade,
        string? sector = null, string? department = null, Division? division = null)
    {
        var employee = new Employee
        {
            EmployeeNumber = number,
            Name = name,
            JobTitle = jobTitle,
            Email = $"{number.ToLowerInvariant()}@kia.example",
            Role = role,
            Grade = grade,
            SectorId = null,
            DepartmentId = null,
            DivisionId = division?.Id,
        };

        // Org placement is resolved against the codes; kept as denormalized ids for grouping.
        employee.SectorId = sector is null ? null : _sectorIdByCode.GetValueOrDefault(sector);
        employee.DepartmentId = department is null ? null : _departmentIdByCode.GetValueOrDefault(department);

        db.Employees.Add(employee);
        _people[number] = employee;
        return employee;
    }

    // ----- Manager assignments ------------------------------------------------------------

    private void BuildManagerAssignments(Dictionary<string, Division> divisions)
    {
        Employee md = _people["MD-001"];

        // Sector heads report to the MD.
        Assign("SEC-INV", md);
        Assign("SEC-OPS", md);

        // Department managers report to their sector head.
        Assign("DEP-INV-EQ", _people["SEC-INV"]);
        Assign("DEP-INV-FI", _people["SEC-INV"]);
        Assign("DEP-OPS-IT", _people["SEC-OPS"]);
        Assign("DEP-OPS-HR", _people["SEC-OPS"]);

        // HR administrator reports to the Operations sector head.
        Assign("HR-001", _people["SEC-OPS"]);

        // Division heads report to their department manager.
        foreach (var divCode in divisions.Keys)
        {
            Assign($"LM-{divCode}", _people[$"DEP-{DepartmentCodeOf(divCode)}"]);
        }

        // Rank-and-file report to their division head.
        foreach (Employee e in _people.Values.Where(p => p.Role == Role.Employee && p.DivisionId is not null))
        {
            if (e.EmployeeNumber == "EMP-MIDYEAR")
            {
                continue; // handled below
            }

            string divCode = divisions.First(d => d.Value.Id == e.DivisionId).Key;
            Assign(e.EmployeeNumber, _people[$"LM-{divCode}"]);
        }

        // Mid-year change: Noura (Jan–Aug) then Bader (Sep–Dec).
        Assign("EMP-MIDYEAR", _people["LM-INV-EQ-LOCAL"], CycleStart, MidYear);
        Assign("EMP-MIDYEAR", _people["LM-INV-EQ-INTL"], MidYear.AddDays(1), CycleEnd);

        // MD-direct report.
        Assign("EMP-CHIEF", md);
    }

    private void Assign(string employeeNumber, Employee manager, DateOnly? start = null, DateOnly? end = null)
    {
        Employee employee = _people[employeeNumber];
        var assignment = new ManagerAssignment
        {
            EmployeeId = employee.Id,
            ManagerId = manager.Id,
            StartDate = start ?? CycleStart,
            EndDate = end,
        };
        employee.ManagerAssignments.Add(assignment);

        if (!_assignmentsByEmployee.TryGetValue(employee.Id, out List<ManagerAssignment>? list))
        {
            _assignmentsByEmployee[employee.Id] = list = [];
        }

        list.Add(assignment);
    }

    // ----- Cycle & evaluations ------------------------------------------------------------

    private void BuildCycle()
    {
        _cycle = new EvaluationCycle
        {
            Name = L("Annual Performance 2025", "تقييم الأداء السنوي 2025"),
            Year = 2025,
            StartDate = CycleStart,
            EndDate = CycleEnd,
            Status = CycleStatus.Active,
        };
        db.EvaluationCycles.Add(_cycle);
    }

    private void BuildEvaluations()
    {
        // Everyone who is evaluated: rank-and-file, division heads and department managers.
        var evaluated = _people.Values
            .Where(p => p.Role is Role.Employee or Role.LineManager or Role.DepartmentManager)
            .OrderBy(p => p.EmployeeNumber)
            .ToList();

        // A deterministic spread of stages and bands so every approver inbox has work and the HR
        // calibration screen shows a real distribution.
        EvaluationStage[] stageCycle =
        [
            EvaluationStage.ManagerEvaluation, EvaluationStage.DepartmentReview,
            EvaluationStage.SectorApproval, EvaluationStage.HrCalibration,
            EvaluationStage.Finalized, EvaluationStage.HrCalibration,
            EvaluationStage.DepartmentReview, EvaluationStage.ManagerEvaluation,
        ];
        RatingBand[] bandCycle =
        [
            RatingBand.Meets, RatingBand.Exceeds, RatingBand.Meets, RatingBand.Outstanding,
            RatingBand.Meets, RatingBand.Exceeds, RatingBand.PartiallyMeets, RatingBand.Meets,
            RatingBand.Unsatisfactory, RatingBand.Meets,
        ];

        int index = 0;
        foreach (Employee employee in evaluated)
        {
            (EvaluationStage stage, RatingBand band) = ScenarioFor(employee, index, stageCycle, bandCycle);
            CreateEvaluation(employee, stage, band);
            index++;
        }

        // Guarantee a visible quota violation in the Investment sector: three Outstanding, finalized.
        ForceOutstanding("LM-INV-FI-GOV");
        ForceOutstanding("DEP-INV-EQ");
        ForceOutstanding("EMP-002");
    }

    private (EvaluationStage, RatingBand) ScenarioFor(
        Employee e, int index, EvaluationStage[] stages, RatingBand[] bands) => e.EmployeeNumber switch
    {
        // Employee persona: sits in the line manager's inbox, ready to be scored and submitted.
        "EMP-000" => (EvaluationStage.ManagerEvaluation, RatingBand.Meets),
        // Mid-year change: manager evaluation done by both managers, now with the department manager.
        "EMP-MIDYEAR" => (EvaluationStage.DepartmentReview, RatingBand.Exceeds),
        // MD-direct report: evaluated by the MD, now in HR calibration.
        "EMP-CHIEF" => (EvaluationStage.HrCalibration, RatingBand.Exceeds),
        _ => (stages[index % stages.Length], bands[index % bands.Length]),
    };

    private void CreateEvaluation(Employee employee, EvaluationStage stage, RatingBand band)
    {
        var evaluation = new Evaluation
        {
            CycleId = _cycle.Id,
            EmployeeId = employee.Id,
            Stage = stage,
        };

        (int competencyRating, int objectiveRating) = RatingsFor(band);
        AddItems(evaluation, competencyRating, objectiveRating);

        // Score once the manager has submitted (department review onward).
        if (stage >= EvaluationStage.DepartmentReview)
        {
            ScoreResult result = scoring.Score(
                ScoredItems(evaluation, EvaluationItemKind.Competency),
                ScoredItems(evaluation, EvaluationItemKind.Objective));
            evaluation.ScorePercent = result.Percent;
            evaluation.WeightedRating = result.WeightedRating;
            evaluation.Band = result.Band;
        }

        BuildApprovalSteps(evaluation, employee, stage);
        _cycle.Evaluations.Add(evaluation);
    }

    private static (int competency, int objective) RatingsFor(RatingBand band) => band switch
    {
        RatingBand.Outstanding => (5, 5),     // 100%
        RatingBand.Exceeds => (4, 4),         // 75%
        RatingBand.Meets => (4, 3),           // 60%
        RatingBand.PartiallyMeets => (3, 3),  // 50%
        _ => (2, 2),                          // 25% Unsatisfactory
    };

    private static void AddItems(Evaluation evaluation, int competencyRating, int objectiveRating)
    {
        (LocalizedText name, decimal weight)[] competencies =
        [
            (L("Job Knowledge", "المعرفة الوظيفية"), 25m),
            (L("Quality of Work", "جودة العمل"), 25m),
            (L("Teamwork", "العمل الجماعي"), 25m),
            (L("Communication", "التواصل"), 25m),
        ];
        (LocalizedText name, decimal weight)[] objectives =
        [
            (L("Deliver annual portfolio targets", "تحقيق أهداف المحفظة السنوية"), 40m),
            (L("Improve process efficiency", "تحسين كفاءة العمليات"), 30m),
            (L("Complete compliance training", "إكمال تدريب الالتزام"), 30m),
        ];

        foreach (var (name, weight) in competencies)
        {
            evaluation.Items.Add(new EvaluationItem
            {
                Kind = EvaluationItemKind.Competency,
                Name = name,
                Weight = weight,
                Rating = competencyRating,
            });
        }

        foreach (var (name, weight) in objectives)
        {
            evaluation.Items.Add(new EvaluationItem
            {
                Kind = EvaluationItemKind.Objective,
                Name = name,
                Weight = weight,
                Rating = objectiveRating,
            });
        }
    }

    private static List<ScoredItem> ScoredItems(Evaluation evaluation, EvaluationItemKind kind) =>
        evaluation.Items
            .Where(i => i.Kind == kind && i.Rating is not null)
            .Select(i => new ScoredItem { Name = i.Name, Weight = i.Weight, Rating = i.Rating!.Value })
            .ToList();

    private void BuildApprovalSteps(Evaluation evaluation, Employee employee, EvaluationStage stage)
    {
        IReadOnlyList<ApprovalStepDefinition> chain = routing.BuildApprovalChain(BuildRoutingRequest(employee));
        int currentIndex = chain.ToList().FindIndex(s => s.Stage == stage);

        for (int i = 0; i < chain.Count; i++)
        {
            ApprovalStepDefinition step = chain[i];
            if (step.Approvers.Count == 0)
            {
                continue; // terminal (Finalized) stage has no approver
            }

            bool completed = currentIndex >= 0 && i < currentIndex;
            foreach (Approver approver in step.Approvers)
            {
                evaluation.ApprovalSteps.Add(new ApprovalStep
                {
                    Order = step.Order,
                    Stage = step.Stage,
                    ApproverId = approver.EmployeeId,
                    Weight = approver.Weight,
                    Decision = completed ? ApprovalDecision.Approve : null,
                    DecidedAt = completed ? DateTimeOffset.UtcNow : null,
                });
            }
        }
    }

    private RoutingRequest BuildRoutingRequest(Employee employee)
    {
        List<ManagerAssignment> assignments = _assignmentsByEmployee.GetValueOrDefault(employee.Id, []);
        var lineManagers = assignments
            .Select(a => new ManagerTenure(a.ManagerId, a.StartDate, a.EndDate ?? CycleEnd))
            .ToList();

        Guid? primaryManagerId = assignments
            .OrderByDescending(a => a.StartDate)
            .Select(a => (Guid?)a.ManagerId)
            .FirstOrDefault();

        Guid? departmentManagerId = ManagerOf(primaryManagerId);
        Guid? sectorHeadId = ManagerOf(departmentManagerId);

        return new RoutingRequest
        {
            LineManagers = lineManagers,
            DepartmentManagerId = departmentManagerId,
            SectorHeadId = sectorHeadId,
            HrCalibratorId = _people["HR-001"].Id,
            ManagingDirectorId = _people["MD-001"].Id,
            ReportsDirectlyToManagingDirector = employee.ReportsDirectlyToManagingDirector,
            CycleStart = CycleStart,
            CycleEnd = CycleEnd,
        };
    }

    private Guid? ManagerOf(Guid? employeeId)
    {
        if (employeeId is null)
        {
            return null;
        }

        return _assignmentsByEmployee.GetValueOrDefault(employeeId.Value, [])
            .OrderByDescending(a => a.StartDate)
            .Select(a => (Guid?)a.ManagerId)
            .FirstOrDefault();
    }

    private void ForceOutstanding(string employeeNumber)
    {
        Employee employee = _people[employeeNumber];
        Evaluation? evaluation = _cycle.Evaluations.FirstOrDefault(e => e.EmployeeId == employee.Id);
        if (evaluation is null)
        {
            return;
        }

        foreach (EvaluationItem item in evaluation.Items)
        {
            item.Rating = 5;
        }

        evaluation.Stage = EvaluationStage.Finalized;
        ScoreResult result = scoring.Score(
            ScoredItems(evaluation, EvaluationItemKind.Competency),
            ScoredItems(evaluation, EvaluationItemKind.Objective));
        evaluation.ScorePercent = result.Percent;
        evaluation.WeightedRating = result.WeightedRating;
        evaluation.Band = result.Band;

        // All approver steps are now complete.
        foreach (ApprovalStep approvalStep in evaluation.ApprovalSteps)
        {
            approvalStep.Decision = ApprovalDecision.Approve;
            approvalStep.DecidedAt = DateTimeOffset.UtcNow;
        }
    }

    // ----- Code helpers -------------------------------------------------------------------

    private readonly Dictionary<string, Guid> _sectorIdByCode = [];
    private readonly Dictionary<string, Guid> _departmentIdByCode = [];

    private static string SectorCodeOf(string divCode) => divCode.Split('-')[0];

    private static string DepartmentCodeOf(string divCode)
    {
        string[] parts = divCode.Split('-');
        return $"{parts[0]}-{parts[1]}";
    }
}
