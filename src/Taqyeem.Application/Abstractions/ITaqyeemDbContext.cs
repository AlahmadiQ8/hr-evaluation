using Microsoft.EntityFrameworkCore;
using Taqyeem.Domain.Evaluations;
using Taqyeem.Domain.Organization;
using Taqyeem.Domain.People;

namespace Taqyeem.Application.Abstractions;

/// <summary>
/// Abstraction over the persistence context so the application layer can query and persist
/// without depending on a concrete EF Core provider. Implemented by the Infrastructure DbContext.
/// </summary>
public interface ITaqyeemDbContext
{
    DbSet<Sector> Sectors { get; }
    DbSet<Department> Departments { get; }
    DbSet<Division> Divisions { get; }
    DbSet<Employee> Employees { get; }
    DbSet<ManagerAssignment> ManagerAssignments { get; }
    DbSet<EvaluationCycle> EvaluationCycles { get; }
    DbSet<Evaluation> Evaluations { get; }
    DbSet<EvaluationItem> EvaluationItems { get; }
    DbSet<ApprovalStep> ApprovalSteps { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
