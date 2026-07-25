using Phisio.Application.Admin.ExerciseCategories;
using Phisio.Application.Common;

namespace Phisio.Application.ExerciseCategories;

public interface IExerciseCategoryService
{
    Task<AuthResult<IReadOnlyList<ExerciseCategoryDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ExerciseCategoryDto>> GetByIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ExerciseCategoryDto>> CreateAsync(
        CreateExerciseCategoryDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ExerciseCategoryDto>> UpdateAsync(
        Guid categoryId,
        UpdateExerciseCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> ActivateAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);
}
