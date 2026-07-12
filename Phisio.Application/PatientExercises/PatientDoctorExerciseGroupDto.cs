namespace Phisio.Application.PatientExercises;

public sealed record PatientDoctorExerciseGroupDto(
    string DoctorName,
    IReadOnlyList<PatientTodayExerciseItemDto> Exercises);
