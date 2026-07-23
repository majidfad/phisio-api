using Phisio.Application.Common;
using Phisio.Application.Exercises;

namespace Phisio.Application.DoctorExercises;

public interface IDoctorExerciseService
{
    Task<AuthResult<IReadOnlyList<DoctorExerciseDto>>> GetLibraryAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<ExerciseDto>>> GetCatalogAsync(
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
