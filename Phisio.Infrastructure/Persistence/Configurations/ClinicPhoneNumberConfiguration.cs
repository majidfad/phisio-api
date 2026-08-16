using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ClinicPhoneNumberConfiguration : IEntityTypeConfiguration<ClinicPhoneNumber>
{
    public void Configure(EntityTypeBuilder<ClinicPhoneNumber> builder)
    {
        builder.ToTable("clinic_phone_numbers");

        builder.HasKey(phoneNumber => phoneNumber.ClinicPhoneNumberId);

        builder.Property(phoneNumber => phoneNumber.ClinicPhoneNumberId)
            .ValueGeneratedNever();

        builder.Property(phoneNumber => phoneNumber.ClinicId)
            .IsRequired();

        builder.Property(phoneNumber => phoneNumber.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(phoneNumber => phoneNumber.NormalizedPhoneNumber)
            .IsRequired()
            .HasMaxLength(21);

        builder.HasIndex(phoneNumber => phoneNumber.ClinicId);

        builder.HasIndex(phoneNumber => phoneNumber.NormalizedPhoneNumber)
            .IsUnique();

        builder.HasOne(phoneNumber => phoneNumber.Clinic)
            .WithMany(clinic => clinic.PhoneNumbers)
            .HasForeignKey(phoneNumber => phoneNumber.ClinicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
