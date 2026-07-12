namespace Phisio.Application.DoctorPatients;

public sealed record DoctorPatientExerciseDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string ExerciseName,
    string? VideoUrl,
    DateTime AssignedAt,
    DateOnly ScheduledDate);
