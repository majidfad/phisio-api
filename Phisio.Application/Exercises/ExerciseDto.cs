namespace Phisio.Application.Exercises;

public sealed record ExerciseDto(
    Guid ExerciseId,
    string Title,
    string Description,
    string? VideoUrl,
    DateTime CreatedAt,
    bool IsEnabled = true);
