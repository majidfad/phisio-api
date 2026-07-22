using Phisio.Domain.Enums;

namespace Phisio.Application.PatientExercises;

public sealed record PatientTodayExerciseItemDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string Title,
    string? VideoUrl,
    ExerciseMediaType MediaType,
    string Instructions,
    DateOnly ScheduledDate,
    bool CompletedToday,
    int? Sets,
    string? Reps,
    int? HoldSeconds,
    int? RestSeconds,
    ExerciseSide Side,
    string? PatientCue);
