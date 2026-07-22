using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorPatients;

public sealed record AssignPatientExerciseItem(
    Guid ExerciseId,
    int? Sets,
    string? Reps,
    int? HoldSeconds,
    int? RestSeconds,
    ExerciseSide Side,
    string? ClinicianNote,
    string? PatientCue);

public sealed record AssignPatientExercisesRequest(
    IReadOnlyList<AssignPatientExerciseItem> Items,
    IReadOnlyList<DateOnly> ScheduledDates);
