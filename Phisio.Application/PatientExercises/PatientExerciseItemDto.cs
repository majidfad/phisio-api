using Phisio.Domain.Enums;

namespace Phisio.Application.PatientExercises;

public sealed record PatientExerciseItemDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string Title,
    string? VideoUrl,
    ExerciseMediaType MediaType,
    string Instructions,
    DateTime AssignedAt,
    DateOnly ScheduledDate,
    bool CompletedToday,
    int? Sets,
    string? Reps,
    string? PatientCue,
    Guid ClinicId,
    string ClinicName);
