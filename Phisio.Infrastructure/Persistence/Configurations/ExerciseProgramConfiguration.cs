using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ExerciseProgramConfiguration : IEntityTypeConfiguration<ExerciseProgram>
{
    public void Configure(EntityTypeBuilder<ExerciseProgram> builder)
    {
        builder.ToTable("exercise_programs");

        builder.HasKey(p => p.ProgramId);

        builder.Property(p => p.ProgramId).ValueGeneratedNever();
        builder.Property(p => p.DoctorId).IsRequired();
        builder.Property(p => p.PatientId).IsRequired();
        builder.Property(p => p.StartDate).IsRequired().HasColumnType("date");
        builder.Property(p => p.EndDate).IsRequired().HasColumnType("date");
        builder.Property(p => p.CadenceType)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseProgramCadenceType.DaysOfWeek);
        builder.Property(p => p.DaysOfWeekMask).IsRequired();
        builder.Property(p => p.IntervalDays);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(p => new { p.DoctorId, p.PatientId });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
