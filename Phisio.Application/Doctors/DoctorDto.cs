namespace Phisio.Application.Doctors;

public sealed record DoctorDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    DateTime CreatedAt,
    string? Email = null,
    bool IsEnabled = true);
