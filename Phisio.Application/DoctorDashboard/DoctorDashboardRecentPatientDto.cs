namespace Phisio.Application.DoctorDashboard;

public sealed record DoctorDashboardRecentPatientDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber,
    Guid ClinicId,
    string ClinicName);
