using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Taqyeem.Domain.Common;
using Taqyeem.Domain.Evaluations;
using Taqyeem.Domain.Organization;
using Taqyeem.Domain.People;

namespace Taqyeem.Infrastructure.Persistence;

public class TaqyeemDbContext(DbContextOptions<TaqyeemDbContext> options) : DbContext(options)
{
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ManagerAssignment> ManagerAssignments => Set<ManagerAssignment>();
    public DbSet<EvaluationCycle> EvaluationCycles => Set<EvaluationCycle>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationItem> EvaluationItems => Set<EvaluationItem>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable strings and give all money-like decimals a sane precision.
        configurationBuilder.Properties<Role>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<EvaluationStage>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<RatingBand>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<CycleStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<EvaluationItemKind>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ApprovalDecision>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<decimal>().HavePrecision(9, 4);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Sector>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            OwnLocalized(e, x => x.Name, "Name");
            e.HasMany(x => x.Departments).WithOne(x => x.Sector!)
                .HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Department>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            OwnLocalized(e, x => x.Name, "Name");
            e.HasMany(x => x.Divisions).WithOne(x => x.Department!)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Division>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            OwnLocalized(e, x => x.Name, "Name");
            e.HasMany(x => x.Employees).WithOne(x => x.Division!)
                .HasForeignKey(x => x.DivisionId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Employee>(e =>
        {
            e.Property(x => x.EmployeeNumber).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.EmployeeNumber).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            OwnLocalized(e, x => x.Name, "Name");
            OwnLocalized(e, x => x.JobTitle, "JobTitle");
            e.HasMany(x => x.ManagerAssignments).WithOne(x => x.Employee!)
                .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ManagerAssignment>(e =>
        {
            e.HasOne(x => x.Manager).WithMany()
                .HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<EvaluationCycle>(e =>
        {
            OwnLocalized(e, x => x.Name, "Name");
            e.HasMany(x => x.Evaluations).WithOne(x => x.Cycle!)
                .HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Evaluation>(e =>
        {
            e.HasOne(x => x.Employee).WithMany()
                .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne(x => x.Evaluation!)
                .HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ApprovalSteps).WithOne(x => x.Evaluation!)
                .HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CycleId, x.EmployeeId }).IsUnique();
        });

        model.Entity<EvaluationItem>(e => OwnLocalized(e, x => x.Name, "Name"));

        model.Entity<ApprovalStep>(e =>
        {
            e.Property(x => x.Comment).HasMaxLength(1024);
            e.HasOne(x => x.Approver).WithMany()
                .HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void OwnLocalized<T>(EntityTypeBuilder<T> entity, Expression<Func<T, LocalizedText?>> navigation, string columnPrefix)
        where T : class
    {
        entity.OwnsOne(navigation, owned =>
        {
            owned.Property(p => p.En).HasColumnName($"{columnPrefix}_En").HasMaxLength(256).IsRequired();
            owned.Property(p => p.Ar).HasColumnName($"{columnPrefix}_Ar").HasMaxLength(256).IsRequired();
        });
        entity.Navigation(navigation).IsRequired();
    }
}
