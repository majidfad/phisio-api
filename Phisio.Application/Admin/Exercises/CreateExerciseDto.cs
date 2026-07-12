namespace Phisio.Application.Admin.Exercises;

public sealed class CreateExerciseDto
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }
}
