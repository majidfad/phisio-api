using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorPatients;

public sealed record PatientExerciseHistoryResponse(
    PatientExerciseHistoryPatientDto Patient,
    PatientExerciseHistorySummaryDto Summary,
    IReadOnlyList<PatientExerciseHistoryDayDto> DailyHistory,
    int TotalDays,
    int Page,
    int PageSize);

public sealed record PatientExerciseHistoryPatientDto(
    Guid PatientId,
    string PatientName,
    string PhoneNumber);

public sealed record PatientExerciseHistorySummaryDto(
    int AssignedExerciseCount,
    int CompletedDaysCount,
    int MissedDaysCount,
    int AdherencePercentage);

public sealed record PatientExerciseHistoryDayDto(
    DateOnly Date,
    int CompletedExerciseCount,
    bool IsCompleted,
    IReadOnlyList<PatientExerciseHistoryExerciseDto> Exercises,
    int? ImprovementScore,
    int? HardnessScore,
    string? Comment);

public sealed record PatientExerciseHistoryExerciseDto(
    Guid UserExerciseId,
    Guid ExerciseId,
    string Title,
    bool IsCompleted,
    int? Sets,
    string? Reps,
    int? HoldSeconds,
    int? RestSeconds,
    ExerciseSide Side,
    string? ClinicianNote,
    string? PatientCue);
