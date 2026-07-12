using Phisio.Domain.Entities;

namespace Phisio.Tests.TestDataBuilder;

internal static class DoctorProfileBuilder
{
    public static DoctorProfile Create(
        Guid doctorId,
        string specialty = "Orthopedics",
        string medicalLicenseNumber = "MD-12345",
        string clinicAddress = "123 Clinic St",
        Guid? id = null,
        DateTime? createdAt = null) =>
        new()
        {
            DoctorProfileId = id ?? Guid.NewGuid(),
            DoctorId = doctorId,
            Specialty = specialty,
            MedicalLicenseNumber = medicalLicenseNumber,
            ClinicAddress = clinicAddress,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
}

internal static class DoctorTestDataBuilder
{
    public static Phisio.Application.Admin.Doctors.CreateAdminDoctorDto CreateDto(
        string name = "Dr. Jane Smith",
        string phoneNumber = "+15551234567",
        string? email = "jane.smith@example.com",
        string specialty = "Orthopedics",
        string medicalLicenseNumber = "MD-12345",
        string clinicAddress = "123 Clinic St") =>
        new()
        {
            Name = name,
            PhoneNumber = phoneNumber,
            Email = email,
            Specialty = specialty,
            MedicalLicenseNumber = medicalLicenseNumber,
            ClinicAddress = clinicAddress,
        };

    public static Phisio.Application.Admin.Doctors.UpdateAdminDoctorDto UpdateDto(
        string name = "Dr. Updated",
        string phoneNumber = "+15559876543",
        string? email = "updated@example.com",
        string specialty = "Physiotherapy",
        string medicalLicenseNumber = "MD-67890",
        string clinicAddress = "456 Health Ave") =>
        new()
        {
            Name = name,
            PhoneNumber = phoneNumber,
            Email = email,
            Specialty = specialty,
            MedicalLicenseNumber = medicalLicenseNumber,
            ClinicAddress = clinicAddress,
        };
}
