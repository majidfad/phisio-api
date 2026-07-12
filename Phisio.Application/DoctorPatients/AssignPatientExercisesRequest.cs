namespace Phisio.Application.DoctorPatients;

public sealed record AssignPatientExercisesRequest(
    IReadOnlyList<Guid> ExerciseIds,
    IReadOnlyList<DateOnly> ScheduledDates);
