using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorPatients;

public sealed record CreateExerciseProgramRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    ExerciseProgramCadenceType CadenceType,
    int DaysOfWeekMask,
    int? IntervalDays,
    IReadOnlyList<AssignPatientExerciseItem> Items);

public sealed record UpdateExerciseProgramRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    ExerciseProgramCadenceType CadenceType,
    int DaysOfWeekMask,
    int? IntervalDays,
    IReadOnlyList<AssignPatientExerciseItem> Items);

public sealed record ExerciseProgramDto(
    Guid ProgramId,
    Guid PatientId,
    DateOnly StartDate,
    DateOnly EndDate,
    ExerciseProgramCadenceType CadenceType,
    int DaysOfWeekMask,
    int? IntervalDays,
    DateTime CreatedAt,
    IReadOnlyList<ExerciseProgramExerciseDto> Exercises,
    int UpcomingAssignmentCount,
    int PastAssignmentCount);

public sealed record ExerciseProgramExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    int? Sets,
    string? Reps,
    int? HoldSeconds,
    int? RestSeconds,
    ExerciseSide Side,
    string? ClinicianNote,
    string? PatientCue);

public sealed record CreateExerciseProgramResultDto(
    Guid ProgramId,
    int AssignedCount);

public sealed record DoctorPatientOverviewDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber,
    DateTime LinkedAt,
    DateTime? PatientRegisteredAt,
    PatientExerciseHistorySummaryDto Summary,
    IReadOnlyList<ExerciseProgramDto> Programs,
    int ActiveExerciseCountToday);
