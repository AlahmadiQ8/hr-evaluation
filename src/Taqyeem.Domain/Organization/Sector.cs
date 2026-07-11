using Taqyeem.Domain.Common;

namespace Taqyeem.Domain.Organization;

/// <summary>Top-level organizational unit (e.g. Investment, Operations).</summary>
public class Sector
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required LocalizedText Name { get; set; }

    public List<Department> Departments { get; } = [];
}
