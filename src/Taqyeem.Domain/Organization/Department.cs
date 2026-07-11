using Taqyeem.Domain.Common;

namespace Taqyeem.Domain.Organization;

/// <summary>A department within a <see cref="Sector"/>.</summary>
public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required LocalizedText Name { get; set; }

    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }

    public List<Division> Divisions { get; } = [];
}
