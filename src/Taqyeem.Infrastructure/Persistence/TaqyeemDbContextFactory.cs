using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Taqyeem.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the model for migrations without the
/// running app or a real connection string (migrations do not connect to the database).
/// </summary>
public sealed class TaqyeemDbContextFactory : IDesignTimeDbContextFactory<TaqyeemDbContext>
{
    public TaqyeemDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<TaqyeemDbContext> options = new DbContextOptionsBuilder<TaqyeemDbContext>()
            .UseSqlServer("Server=localhost;Database=evaluationdb;Trusted_Connection=False;TrustServerCertificate=True;")
            .Options;

        return new TaqyeemDbContext(options);
    }
}
