namespace Phisio.Application.PatientExercises;

public sealed record PatientTodayExercisesResponse(
    IReadOnlyList<PatientDoctorExerciseGroupDto> DoctorGroups);
