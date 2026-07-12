using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;

namespace Phisio.Application.Exercises;

public interface IExerciseService
{
    Task<AuthResult<IReadOnlyList<ExerciseDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ExerciseDto>> GetByIdAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ExerciseDto>> CreateAsync(
        CreateExerciseDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ExerciseDto>> UpdateAsync(
        Guid exerciseId,
        UpdateExerciseRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> ActivateAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default);
}
