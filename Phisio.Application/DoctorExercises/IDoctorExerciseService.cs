using Phisio.Application.Common;

namespace Phisio.Application.DoctorExercises;

public interface IDoctorExerciseService
{
    Task<AuthResult<IReadOnlyList<DoctorExerciseDto>>> GetExercisesAsync(
        Guid doctorId,
        DoctorExerciseScope scope = DoctorExerciseScope.All,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorExerciseDto>> CreateAsync(
        Guid doctorId,
        CreateDoctorExerciseRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorExerciseDto>> UpdateAsync(
        Guid doctorId,
        Guid exerciseId,
        UpdateDoctorExerciseRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(
        Guid doctorId,
        Guid exerciseId,
        CancellationToken cancellationToken = default);
}
