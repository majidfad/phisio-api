using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorPatients;

public sealed record DoctorPatientExerciseDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string ExerciseName,
    string? VideoUrl,
    ExerciseMediaType MediaType,
    DateTime AssignedAt,
    DateOnly ScheduledDate,
    int? Sets,
    string? Reps,
    int? HoldSeconds,
    int? RestSeconds,
    ExerciseSide Side,
    string? ClinicianNote,
    string? PatientCue);
