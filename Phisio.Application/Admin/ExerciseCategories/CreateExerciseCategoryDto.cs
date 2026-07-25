namespace Phisio.Application.Admin.ExerciseCategories;

public sealed class CreateExerciseCategoryDto
{
    public string NameFa { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
