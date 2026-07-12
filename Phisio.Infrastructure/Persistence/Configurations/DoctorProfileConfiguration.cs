using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class DoctorProfileConfiguration : IEntityTypeConfiguration<DoctorProfile>
{
    public void Configure(EntityTypeBuilder<DoctorProfile> builder)
    {
        builder.ToTable("doctor_profiles");

        builder.HasKey(profile => profile.DoctorProfileId);

        builder.Property(profile => profile.DoctorProfileId)
            .ValueGeneratedNever();

        builder.Property(profile => profile.Specialty)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(profile => profile.MedicalLicenseNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(profile => profile.ClinicAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(profile => profile.DoctorId)
            .IsUnique();

        builder.HasIndex(profile => profile.MedicalLicenseNumber)
            .IsUnique()
            .HasFilter("\"MedicalLicenseNumber\" <> ''");

        builder.HasOne<ApplicationUser>()
            .WithOne(user => user.DoctorProfile)
            .HasForeignKey<DoctorProfile>(profile => profile.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
