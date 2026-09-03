namespace Phisio.Domain.Entities;

public class ExerciseCategoryLink
{
    public Guid ExerciseId { get; set; }

    public Guid ExerciseCategoryId { get; set; }

    public Exercise Exercise { get; set; } = null!;

    public ExerciseCategory Category { get; set; } = null!;
}
