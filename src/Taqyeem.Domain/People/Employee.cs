using Taqyeem.Domain.Common;
using Taqyeem.Domain.Organization;

namespace Taqyeem.Domain.People;

/// <summary>A (fictional) employee. Names and job titles are bilingual.</summary>
public class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string EmployeeNumber { get; set; }
    public required LocalizedText Name { get; set; }
    public required LocalizedText JobTitle { get; set; }
    public required string Email { get; set; }

    /// <summary>Pay grade; higher is more senior.</summary>
    public int Grade { get; set; }

    /// <summary>Primary role, used for authorization and as a persona in demo mode.</summary>
    public Role Role { get; set; } = Role.Employee;

    /// <summary>When true, the employee is evaluated directly by the Managing Director.</summary>
    public bool ReportsDirectlyToManagingDirector { get; set; }

    // Organizational placement. Nullable because unit heads and the MD sit above a division.
    public Guid? SectorId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DivisionId { get; set; }
    public Division? Division { get; set; }

    /// <summary>Manager assignments over time; more than one models a mid-year manager change.</summary>
    public List<ManagerAssignment> ManagerAssignments { get; } = [];
}
