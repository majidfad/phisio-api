using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class PatientVisitConfiguration : IEntityTypeConfiguration<PatientVisit>
{
    public void Configure(EntityTypeBuilder<PatientVisit> builder)
    {
        builder.ToTable("patient_visits");

        builder.HasKey(v => v.PatientVisitId);

        builder.Property(v => v.PatientVisitId)
            .ValueGeneratedNever();

        builder.Property(v => v.PatientId)
            .IsRequired();

        builder.Property(v => v.DoctorId)
            .IsRequired();

        builder.Property(v => v.ClinicId)
            .IsRequired();

        builder.Property(v => v.VisitAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(v => v.VisitType)
            .HasColumnType("smallint");

        builder.Property(v => v.PatientCondition)
            .HasColumnType("smallint");

        builder.Property(v => v.DoctorNotes)
            .HasMaxLength(2000);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(v => new { v.PatientId, v.VisitAt });
        builder.HasIndex(v => new { v.DoctorId, v.VisitAt });
        builder.HasIndex(v => new { v.ClinicId, v.VisitAt });

        builder.HasIndex(v => v.PatientId);
        builder.HasIndex(v => v.DoctorId);
        builder.HasIndex(v => v.ClinicId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Clinic)
            .WithMany()
            .HasForeignKey(v => v.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

