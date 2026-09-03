namespace Phisio.Application.PatientExercises;

public sealed record PatientDoctorExerciseGroupDto(
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    IReadOnlyList<PatientTodayExerciseItemDto> Exercises);
