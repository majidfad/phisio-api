namespace Phisio.Application.DoctorExercises;

public sealed record DoctorExerciseDto(
    Guid ExerciseId,
    string Title,
    string? Description,
    string? VideoUrl);
