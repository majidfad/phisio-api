namespace Phisio.Application.PatientExercises;

public sealed record PatientExercisesResponse(
    string? DoctorName,
    IReadOnlyList<PatientExerciseItemDto> Exercises);
