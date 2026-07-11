using Taqyeem.Application.Routing;
using Taqyeem.Domain.Evaluations;
using static Taqyeem.Domain.Evaluations.EvaluationStage;

namespace Taqyeem.Application.Tests.Routing;

[TestClass]
public sealed class RoutingEngineTests
{
    private readonly RoutingEngine _engine = new();

    private static readonly Guid LineManager = Guid.NewGuid();
    private static readonly Guid ManagerA = Guid.NewGuid();
    private static readonly Guid ManagerB = Guid.NewGuid();
    private static readonly Guid DeptManager = Guid.NewGuid();
    private static readonly Guid SectorHead = Guid.NewGuid();
    private static readonly Guid Hr = Guid.NewGuid();
    private static readonly Guid ManagingDirector = Guid.NewGuid();

    private static readonly DateOnly CycleStart = new(2025, 1, 1);
    private static readonly DateOnly CycleEnd = new(2025, 12, 31);

    private static RoutingRequest Request(
        IReadOnlyList<ManagerTenure>? lineManagers = null,
        Guid? deptManager = null,
        Guid? sectorHead = null,
        Guid? hr = null,
        Guid? managingDirector = null,
        bool reportsToMd = false) =>
        new()
        {
            LineManagers = lineManagers ?? [new ManagerTenure(LineManager, CycleStart, CycleEnd)],
            DepartmentManagerId = deptManager,
            SectorHeadId = sectorHead,
            HrCalibratorId = hr,
            ManagingDirectorId = managingDirector,
            ReportsDirectlyToManagingDirector = reportsToMd,
            CycleStart = CycleStart,
            CycleEnd = CycleEnd,
        };

    [TestMethod]
    public void NormalChain_ClimbsTheHierarchy()
    {
        var chain = _engine.BuildApprovalChain(
            Request(deptManager: DeptManager, sectorHead: SectorHead, hr: Hr));

        CollectionAssert.AreEqual(
            new[] { ManagerEvaluation, DepartmentReview, SectorApproval, HrCalibration, Finalized },
            chain.Select(s => s.Stage).ToArray());

        // Orders are consecutive from 1.
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, chain.Select(s => s.Order).ToArray());
        Assert.AreEqual(LineManager, chain[0].Approvers.Single().EmployeeId);
        Assert.AreEqual(DeptManager, chain[1].Approvers.Single().EmployeeId);
    }

    [TestMethod]
    public void ManagingDirectorDirectReport_SkipsDepartmentAndSector()
    {
        var chain = _engine.BuildApprovalChain(
            Request(lineManagers: [], reportsToMd: true, managingDirector: ManagingDirector, hr: Hr));

        CollectionAssert.AreEqual(
            new[] { ManagerEvaluation, HrCalibration, Finalized },
            chain.Select(s => s.Stage).ToArray());
        Assert.AreEqual(ManagingDirector, chain[0].Approvers.Single().EmployeeId);
    }

    [TestMethod]
    public void MidYearManagerChange_SplitsEvaluationByTenure()
    {
        var chain = _engine.BuildApprovalChain(Request(
            lineManagers:
            [
                new ManagerTenure(ManagerA, new DateOnly(2025, 1, 1), new DateOnly(2025, 8, 31)),
                new ManagerTenure(ManagerB, new DateOnly(2025, 9, 1), new DateOnly(2025, 12, 31)),
            ],
            deptManager: DeptManager, sectorHead: SectorHead, hr: Hr));

        var managerStep = chain.Single(s => s.Stage == ManagerEvaluation);
        Assert.HasCount(2, managerStep.Approvers);

        Approver a = managerStep.Approvers.Single(x => x.EmployeeId == ManagerA);
        Approver b = managerStep.Approvers.Single(x => x.EmployeeId == ManagerB);

        Assert.IsGreaterThan(b.Weight, a.Weight, "Manager A managed the employee longer, so should weigh more.");
        Assert.IsTrue(a.Weight is > 0.66m and < 0.67m, $"Manager A weight was {a.Weight}.");
        Assert.AreEqual(1.0m, Math.Round(a.Weight + b.Weight, 1));
    }

    [TestMethod]
    public void ApproverWhoAlreadyActed_IsNotAskedAgain()
    {
        // The line manager is also the department manager => department review is skipped.
        var chain = _engine.BuildApprovalChain(
            Request(deptManager: LineManager, sectorHead: SectorHead, hr: Hr));

        CollectionAssert.AreEqual(
            new[] { ManagerEvaluation, SectorApproval, HrCalibration, Finalized },
            chain.Select(s => s.Stage).ToArray());
    }

    [TestMethod]
    public void MissingManagementLevels_AreSkipped()
    {
        var chain = _engine.BuildApprovalChain(Request(hr: Hr));

        CollectionAssert.AreEqual(
            new[] { ManagerEvaluation, HrCalibration, Finalized },
            chain.Select(s => s.Stage).ToArray());
    }

    [TestMethod]
    public void HrCalibration_IsAddedEvenIfHrAlreadyActedAtAnotherLevel()
    {
        // HR calibrator is also the sector head; HR calibration must still occur.
        var chain = _engine.BuildApprovalChain(
            Request(deptManager: DeptManager, sectorHead: SectorHead, hr: SectorHead));

        Assert.IsTrue(chain.Any(s => s.Stage == HrCalibration));
        Assert.AreEqual(SectorHead, chain.Single(s => s.Stage == HrCalibration).Approvers.Single().EmployeeId);
    }

    [TestMethod]
    public void NextStage_Approve_AdvancesAlongTheChain()
    {
        var chain = _engine.BuildApprovalChain(
            Request(deptManager: DeptManager, sectorHead: SectorHead, hr: Hr));

        Assert.AreEqual(DepartmentReview, _engine.NextStage(chain, ManagerEvaluation, ApprovalDecision.Approve));
        Assert.AreEqual(HrCalibration, _engine.NextStage(chain, SectorApproval, ApprovalDecision.Approve));
        Assert.AreEqual(Finalized, _engine.NextStage(chain, HrCalibration, ApprovalDecision.Approve));
    }

    [TestMethod]
    public void NextStage_Return_GoesBackToManagerEvaluation()
    {
        var chain = _engine.BuildApprovalChain(
            Request(deptManager: DeptManager, sectorHead: SectorHead, hr: Hr));

        Assert.AreEqual(ManagerEvaluation, _engine.NextStage(chain, SectorApproval, ApprovalDecision.Return));
    }

    [TestMethod]
    public void NextStage_FromFinalized_Throws()
    {
        var chain = _engine.BuildApprovalChain(Request(hr: Hr));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => _engine.NextStage(chain, Finalized, ApprovalDecision.Approve));
    }

    [TestMethod]
    public void NextStage_StageNotInChain_Throws()
    {
        // MD-direct chain has no DepartmentReview stage.
        var chain = _engine.BuildApprovalChain(
            Request(lineManagers: [], reportsToMd: true, managingDirector: ManagingDirector, hr: Hr));

        Assert.ThrowsExactly<ArgumentException>(
            () => _engine.NextStage(chain, DepartmentReview, ApprovalDecision.Approve));
    }

    [TestMethod]
    public void NoManagersAndNotMdDirect_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => _engine.BuildApprovalChain(Request(lineManagers: [], hr: Hr)));
    }

    [TestMethod]
    public void MdDirectWithoutManagingDirectorId_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => _engine.BuildApprovalChain(Request(lineManagers: [], reportsToMd: true, hr: Hr)));
    }

    [TestMethod]
    public void ManagerWeights_SingleManagerForWholeCycle_IsOne()
    {
        var weights = _engine.ManagerWeights(
            [new ManagerTenure(LineManager, CycleStart, CycleEnd)], CycleStart, CycleEnd);

        Assert.AreEqual(1.0m, weights.Single().Weight);
    }

    [TestMethod]
    public void ManagerWeights_NoOverlapWithCycle_Throws()
    {
        // Tenure ends before the cycle begins.
        Assert.ThrowsExactly<ArgumentException>(() => _engine.ManagerWeights(
            [new ManagerTenure(LineManager, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30))],
            CycleStart, CycleEnd));
    }
}
