namespace Phisio.Application.DoctorDashboard;

public sealed record DoctorDashboardDto(
    int PatientsCount,
    int PendingRequestsCount,
    int AssignedExercisesCount,
    int CompletedExercisesCount,
    int FeedbackCount,
    IReadOnlyList<DoctorDashboardRecentPatientDto> RecentPatients);
