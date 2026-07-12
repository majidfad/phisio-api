namespace Phisio.Application.PatientExercises;

public sealed record PatientExerciseItemDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string Title,
    string? VideoUrl,
    DateTime AssignedAt,
    DateOnly ScheduledDate,
    bool IsCompletedToday);
