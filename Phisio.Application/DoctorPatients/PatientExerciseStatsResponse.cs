namespace Phisio.Application.DoctorPatients;

public sealed record PatientExerciseStatsResponse(
    DateOnly From,
    DateOnly To,
    PatientExerciseStatsSummaryDto Summary,
    IReadOnlyList<PatientExerciseStatsDailyDto> Daily,
    IReadOnlyList<PatientExerciseStatsWeeklyDto> Weekly,
    IReadOnlyList<PatientExerciseStatsExerciseDto> Exercises);

public sealed record PatientExerciseStatsSummaryDto(
    int ScheduledDays,
    int CompletedDays,
    int MissedDays,
    int AdherencePercentage,
    int AssignedExerciseCount,
    int CompletedExerciseCount,
    int ExerciseCompletionPercentage,
    double? AverageImprovementScore,
    double? AverageHardnessScore,
    int FeedbackDayCount);

public sealed record PatientExerciseStatsDailyDto(
    DateOnly Date,
    int ScheduledCount,
    int CompletedCount,
    bool IsCompleted,
    int? ImprovementScore,
    int? HardnessScore);

public sealed record PatientExerciseStatsWeeklyDto(
    DateOnly WeekStart,
    int ScheduledDays,
    int CompletedDays,
    int AdherencePercentage);

public sealed record PatientExerciseStatsExerciseDto(
    Guid ExerciseId,
    string Title,
    int AssignedCount,
    int CompletedCount,
    int CompletionPercentage);
