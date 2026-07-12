using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ExerciseCompletionConfiguration : IEntityTypeConfiguration<ExerciseCompletion>
{
    public void Configure(EntityTypeBuilder<ExerciseCompletion> builder)
    {
        builder.ToTable("exercise_completions");

        builder.HasKey(ec => ec.ExerciseCompletionId);

        builder.Property(ec => ec.ExerciseCompletionId)
            .ValueGeneratedNever();

        builder.Property(ec => ec.UserExerciseId)
            .IsRequired();

        builder.Property(ec => ec.PatientId)
            .IsRequired();

        builder.Property(ec => ec.DoctorId)
            .IsRequired();

        builder.Property(ec => ec.ExerciseId)
            .IsRequired();

        builder.Property(ec => ec.CompletionDate)
            .IsRequired();

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(ec => new { ec.UserExerciseId, ec.CompletionDate })
            .IsUnique()
            .HasDatabaseName("ix_exercise_completions_user_exercise_id_completion_date");

        builder.HasIndex(ec => ec.PatientId);
        builder.HasIndex(ec => ec.DoctorId);
        builder.HasIndex(ec => ec.ExerciseId);
        builder.HasIndex(ec => ec.CompletionDate);

        builder.HasOne<UserExercise>()
            .WithMany()
            .HasForeignKey(ec => ec.UserExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
