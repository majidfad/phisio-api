using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class UserExerciseConfiguration : IEntityTypeConfiguration<UserExercise>
{
    public void Configure(EntityTypeBuilder<UserExercise> builder)
    {
        builder.ToTable("user_exercises");

        builder.HasKey(ue => ue.UserExerciseId);

        builder.Property(ue => ue.UserExerciseId)
            .ValueGeneratedNever();

        builder.Property(ue => ue.DoctorId)
            .IsRequired();

        builder.Property(ue => ue.PatientId)
            .IsRequired();

        builder.Property(ue => ue.ExerciseId)
            .IsRequired();

        builder.Property(ue => ue.AssignedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(ue => ue.ScheduledDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(ue => ue.Sets);

        builder.Property(ue => ue.Reps)
            .HasMaxLength(50);

        builder.Property(ue => ue.HoldSeconds);

        builder.Property(ue => ue.RestSeconds);

        builder.Property(ue => ue.Side)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseSide.NotApplicable);

        builder.Property(ue => ue.ClinicianNote)
            .HasMaxLength(1000);

        builder.Property(ue => ue.PatientCue)
            .HasMaxLength(500);

        builder.Property(ue => ue.ProgramId);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.Property(ue => ue.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(ue => ue.DoctorId);

        builder.HasIndex(ue => ue.PatientId);

        builder.HasIndex(ue => ue.ExerciseId);

        builder.HasIndex(ue => ue.ProgramId);

        builder.HasIndex(ue => new { ue.PatientId, ue.IsActive });

        builder.HasIndex(ue => new { ue.PatientId, ue.DoctorId, ue.ExerciseId, ue.ScheduledDate })
            .IsUnique()
            .HasFilter("\"IsActive\" = true AND \"IsEnabled\" = true")
            .HasDatabaseName("ix_user_exercises_patient_doctor_exercise_scheduled_active");

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UserExercises)
            .HasForeignKey(ue => ue.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ue => ue.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ue => ue.Exercise)
            .WithMany(e => e.UserExercises)
            .HasForeignKey(ue => ue.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ue => ue.Program)
            .WithMany(p => p.UserExercises)
            .HasForeignKey(ue => ue.ProgramId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
