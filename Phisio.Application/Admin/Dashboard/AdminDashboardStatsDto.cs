namespace Phisio.Application.Admin.Dashboard;

public record AdminDashboardStatsDto(
    int DoctorCount,
    int PatientCount,
    int ExerciseCount);
