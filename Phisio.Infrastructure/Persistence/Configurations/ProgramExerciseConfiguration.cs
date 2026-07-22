using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ProgramExerciseConfiguration : IEntityTypeConfiguration<ProgramExercise>
{
    public void Configure(EntityTypeBuilder<ProgramExercise> builder)
    {
        builder.ToTable("program_exercises");

        builder.HasKey(pe => pe.ProgramExerciseId);

        builder.Property(pe => pe.ProgramExerciseId).ValueGeneratedNever();
        builder.Property(pe => pe.ProgramId).IsRequired();
        builder.Property(pe => pe.ExerciseId).IsRequired();
        builder.Property(pe => pe.Reps).HasMaxLength(50);
        builder.Property(pe => pe.Side)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseSide.NotApplicable);
        builder.Property(pe => pe.ClinicianNote).HasMaxLength(1000);
        builder.Property(pe => pe.PatientCue).HasMaxLength(500);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(pe => pe.ProgramId);
        builder.HasIndex(pe => new { pe.ProgramId, pe.ExerciseId })
            .IsUnique()
            .HasFilter("\"IsEnabled\" = true")
            .HasDatabaseName("ix_program_exercises_program_exercise_enabled");

        builder.HasOne(pe => pe.Program)
            .WithMany(p => p.Exercises)
            .HasForeignKey(pe => pe.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pe => pe.Exercise)
            .WithMany()
            .HasForeignKey(pe => pe.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
