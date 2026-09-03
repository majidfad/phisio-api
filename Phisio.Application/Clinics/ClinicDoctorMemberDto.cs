using Phisio.Domain.Enums;

namespace Phisio.Application.Clinics;

public sealed record ClinicDoctorMemberDto(
    Guid DoctorId,
    string Name,
    string PhoneNumber,
    UserRole Role,
    string Specialty,
    string MedicalLicenseNumber,
    bool IsClinicManager);
