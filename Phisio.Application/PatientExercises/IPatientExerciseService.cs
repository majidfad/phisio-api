using Phisio.Application.Common;

namespace Phisio.Application.PatientExercises;

public interface IPatientExerciseService
{
    Task<AuthResult<PatientExercisesResponse>> GetExercisesAsync(
        Guid patientId,
        DateOnly? scheduledDate = null,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientTodayExercisesResponse>> GetTodayExercisesAsync(
        Guid patientId,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<CompleteExercisesResponse>> CompleteExercisesAsync(
        Guid patientId,
        CompleteExercisesRequest request,
        CancellationToken cancellationToken = default);
}
