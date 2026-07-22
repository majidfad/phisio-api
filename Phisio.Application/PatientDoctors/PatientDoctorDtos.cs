using Phisio.Domain.Enums;

namespace Phisio.Application.PatientDoctors;

public sealed record PatientDoctorDirectoryItemDto(
    Guid DoctorId,
    string Name,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    string PhoneNumber,
    DoctorPatientStatus? RelationshipStatus);

public sealed record PatientDoctorProfileDto(
    Guid DoctorId,
    string Name,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    string PhoneNumber,
    DoctorPatientStatus? RelationshipStatus,
    DateTime? RelationshipCreatedAt);

public sealed record PatientLinkedDoctorDto(
    Guid DoctorId,
    string Name,
    string Specialty,
    string MedicalLicenseNumber,
    string ClinicAddress,
    string PhoneNumber,
    DoctorPatientStatus Status,
    DateTime CreatedAt);
