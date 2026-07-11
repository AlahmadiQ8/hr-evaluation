namespace Taqyeem.Domain.People;

/// <summary>
/// Assigns a manager to an employee for a date range. Overlapping the cycle with more than one
/// assignment represents a mid-year manager change.
/// </summary>
public class ManagerAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid ManagerId { get; set; }
    public Employee? Manager { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Null means the assignment is open-ended (still current).</summary>
    public DateOnly? EndDate { get; set; }
}
