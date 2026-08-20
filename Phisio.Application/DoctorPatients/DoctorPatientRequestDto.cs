namespace Phisio.Application.DoctorPatients;

public sealed record DoctorPatientRequestDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber,
    DateTime RequestedAt,
    Guid ClinicId,
    string ClinicName);
