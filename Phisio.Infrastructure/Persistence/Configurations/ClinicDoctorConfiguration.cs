using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ClinicDoctorConfiguration : IEntityTypeConfiguration<ClinicDoctor>
{
    public void Configure(EntityTypeBuilder<ClinicDoctor> builder)
    {
        builder.ToTable("clinic_doctors");

        builder.HasKey(clinicDoctor => new { clinicDoctor.ClinicId, clinicDoctor.DoctorId });

        builder.Property(clinicDoctor => clinicDoctor.ClinicId)
            .IsRequired();

        builder.Property(clinicDoctor => clinicDoctor.DoctorId)
            .IsRequired();

        builder.HasIndex(clinicDoctor => clinicDoctor.ClinicId);

        builder.HasIndex(clinicDoctor => clinicDoctor.DoctorId);

        builder.HasIndex(clinicDoctor => new { clinicDoctor.ClinicId, clinicDoctor.DoctorId })
            .IsUnique();

        builder.HasOne(clinicDoctor => clinicDoctor.Clinic)
            .WithMany(clinic => clinic.ClinicDoctors)
            .HasForeignKey(clinicDoctor => clinicDoctor.ClinicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(clinicDoctor => clinicDoctor.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
