namespace Phisio.Application.PatientExercises;

public sealed class CompleteExercisesRequest
{
    public IReadOnlyList<Guid> UserExerciseIds { get; set; } = [];
}
