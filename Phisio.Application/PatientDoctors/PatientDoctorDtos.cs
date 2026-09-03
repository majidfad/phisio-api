using Phisio.Domain.Enums;

namespace Phisio.Application.PatientDoctors;

public sealed record PatientDoctorDirectoryClinicDto(
    Guid ClinicId,
    string Name,
    string Address);

public sealed record PatientDoctorDirectoryItemDto(
    Guid DoctorId,
    string Name,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    string PhoneNumber,
    DoctorPatientStatus? RelationshipStatus,
    IReadOnlyList<PatientDoctorDirectoryClinicDto> Clinics);

public sealed record PatientDoctorProfileDto(
    Guid DoctorId,
    string Name,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    string PhoneNumber,
    DoctorPatientStatus? RelationshipStatus,
    DateTime? RelationshipCreatedAt,
    Guid? ClinicId,
    string? ClinicName);

public sealed record PatientLinkedDoctorDto(
    Guid DoctorId,
    string Name,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    string PhoneNumber,
    DoctorPatientStatus Status,
    DateTime CreatedAt,
    Guid ClinicId,
    string ClinicName);
