namespace Phisio.Application.Clinics;

public sealed record ClinicPatientDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber,
    DateTime AssignedAt,
    Guid ClinicId,
    string ClinicName,
    Guid DoctorId,
    string DoctorName);
