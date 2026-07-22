namespace Phisio.Application.PatientExercises;

public sealed record PatientDoctorExerciseGroupDto(
    Guid DoctorId,
    string DoctorName,
    IReadOnlyList<PatientTodayExerciseItemDto> Exercises);
