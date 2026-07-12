namespace Phisio.Application.PatientExercises;

public sealed record CompleteExercisesResponse(
    DateOnly CompletionDate,
    IReadOnlyList<Guid> CreatedUserExerciseIds,
    IReadOnlyList<Guid> SkippedUserExerciseIds);
