using Phisio.Application.ExerciseCategories;
using Phisio.Domain.Enums;

namespace Phisio.Application.Exercises;

public sealed record ExerciseDto(
    Guid ExerciseId,
    string Title,
    string Description,
    string Instructions,
    string? VideoUrl,
    ExerciseMediaType MediaType,
    ExerciseEquipment Equipment,
    ExerciseDifficulty Difficulty,
    Guid? CreatedByDoctorId,
    DateTime CreatedAt,
    IReadOnlyList<ExerciseCategorySummaryDto> Categories,
    bool IsEnabled = true);
