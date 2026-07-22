using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence.Configurations;

namespace Phisio.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Exercise> Exercises => Set<Exercise>();

    public DbSet<Article> Articles => Set<Article>();

    public DbSet<UserExercise> UserExercises => Set<UserExercise>();

    public DbSet<ExerciseProgram> ExercisePrograms => Set<ExerciseProgram>();

    public DbSet<ProgramExercise> ProgramExercises => Set<ProgramExercise>();

    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();

    public DbSet<DoctorPatient> DoctorPatients => Set<DoctorPatient>();

    public DbSet<ExerciseCompletion> ExerciseCompletions => Set<ExerciseCompletion>();

    public DbSet<DailyPatientFeedback> DailyPatientFeedbacks => Set<DailyPatientFeedback>();

    public override int SaveChanges()
    {
        ApplyAuditableTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditableTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditableTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditableTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureIdentityTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilters();
    }

    private void ApplyAuditableTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = utcNow;
            }
        }
    }
}
