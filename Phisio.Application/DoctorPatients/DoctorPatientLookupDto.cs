namespace Phisio.Application.DoctorPatients;

public sealed record DoctorPatientLookupDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber);
