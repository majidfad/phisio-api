namespace Phisio.Application.Assignments;

public sealed record AssignmentDto(
    Guid Id,
    Guid DoctorId,
    Guid PatientId,
    Guid ExerciseId,
    string ExerciseTitle,
    DateTime AssignedAt,
    bool IsActive);
