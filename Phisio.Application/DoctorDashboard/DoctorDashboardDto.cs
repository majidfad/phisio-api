namespace Phisio.Application.DoctorDashboard;

public sealed record DoctorDashboardDto(
    int PatientsCount,
    IReadOnlyList<DoctorDashboardRecentPatientDto> RecentPatients);
