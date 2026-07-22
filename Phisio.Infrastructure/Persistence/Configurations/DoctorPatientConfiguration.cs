using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class DoctorPatientConfiguration : IEntityTypeConfiguration<DoctorPatient>
{
    public void Configure(EntityTypeBuilder<DoctorPatient> builder)
    {
        builder.ToTable("doctor_patients");

        builder.HasKey(dp => new { dp.DoctorId, dp.PatientId });

        builder.Property(dp => dp.DoctorId)
            .IsRequired();

        builder.Property(dp => dp.PatientId)
            .IsRequired();

        builder.Property(dp => dp.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(dp => new { dp.DoctorId, dp.PatientId })
            .IsUnique();

        builder.HasIndex(dp => dp.DoctorId);

        builder.HasIndex(dp => dp.PatientId);

        builder.HasIndex(dp => new { dp.DoctorId, dp.Status });

        builder.HasIndex(dp => new { dp.PatientId, dp.Status });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(dp => dp.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(dp => dp.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
