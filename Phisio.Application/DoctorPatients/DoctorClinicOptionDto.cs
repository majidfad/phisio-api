namespace Phisio.Application.DoctorPatients;

public sealed record DoctorClinicOptionDto(
    Guid ClinicId,
    string Name,
    string Address,
    int ActivePatientCount,
    int PendingRequestCount);
