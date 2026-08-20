namespace Phisio.Application.DoctorPatients;

public sealed record DoctorPatientDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber,
    DateTime AssignedAt,
    Guid ClinicId,
    string ClinicName);
