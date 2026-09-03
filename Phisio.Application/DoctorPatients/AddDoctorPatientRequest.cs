namespace Phisio.Application.DoctorPatients;

public sealed record AddDoctorPatientRequest(Guid PatientId, Guid ClinicId);
