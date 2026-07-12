namespace Phisio.Application.PatientExercises;

public sealed record PatientTodayExerciseItemDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string Title,
    string? VideoUrl,
    DateOnly ScheduledDate,
    bool CompletedToday);
