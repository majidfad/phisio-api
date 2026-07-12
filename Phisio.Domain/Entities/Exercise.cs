using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class Exercise : BaseEntity
{
    public Guid ExerciseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }

    public ICollection<UserExercise> UserExercises { get; set; } = new List<UserExercise>();
}