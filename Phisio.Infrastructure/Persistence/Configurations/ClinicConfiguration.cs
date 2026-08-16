using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.ToTable("clinics");

        builder.HasKey(clinic => clinic.ClinicId);

        builder.Property(clinic => clinic.ClinicId)
            .ValueGeneratedNever();

        builder.Property(clinic => clinic.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(clinic => clinic.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(clinic => clinic.ClinicManagerId)
            .IsRequired();

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(clinic => clinic.ClinicManagerId);

        builder.HasOne<ApplicationUser>()
            .WithMany(user => user.ManagedClinics)
            .HasForeignKey(clinic => clinic.ClinicManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(clinic => clinic.PhoneNumbers)
            .WithOne(phoneNumber => phoneNumber.Clinic)
            .HasForeignKey(phoneNumber => phoneNumber.ClinicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(clinic => clinic.ClinicDoctors)
            .WithOne(clinicDoctor => clinicDoctor.Clinic)
            .HasForeignKey(clinicDoctor => clinicDoctor.ClinicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
