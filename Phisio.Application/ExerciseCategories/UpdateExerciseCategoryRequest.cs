namespace Phisio.Application.ExerciseCategories;

public sealed class UpdateExerciseCategoryRequest
{
    public string NameFa { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
