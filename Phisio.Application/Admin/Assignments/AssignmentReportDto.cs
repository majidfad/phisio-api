namespace Phisio.Application.Admin.Assignments;

public record AssignmentReportDto(
    string PatientName,
    string DoctorName,
    IReadOnlyList<string> ExerciseNames);
