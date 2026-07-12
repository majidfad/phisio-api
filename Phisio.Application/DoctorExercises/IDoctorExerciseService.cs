using Phisio.Application.Common;

namespace Phisio.Application.DoctorExercises;

public interface IDoctorExerciseService
{
    Task<AuthResult<IReadOnlyList<DoctorExerciseDto>>> GetExercisesAsync(
        CancellationToken cancellationToken = default);
}
