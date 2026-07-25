namespace Phisio.Application.ExerciseCategories;

public sealed record ExerciseCategoryDto(
    Guid ExerciseCategoryId,
    string NameFa,
    string NameEn,
    int SortOrder,
    DateTime CreatedAt,
    bool IsEnabled = true);

public sealed record ExerciseCategorySummaryDto(
    Guid ExerciseCategoryId,
    string NameFa,
    string NameEn);
