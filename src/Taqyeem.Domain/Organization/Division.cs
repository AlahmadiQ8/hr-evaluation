using Taqyeem.Domain.Common;
using Taqyeem.Domain.People;

namespace Taqyeem.Domain.Organization;

/// <summary>A division within a <see cref="Department"/>; the unit employees belong to.</summary>
public class Division
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required LocalizedText Name { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public List<Employee> Employees { get; } = [];
}
