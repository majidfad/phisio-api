using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.ExerciseCategories;
using Phisio.Application.Exercises;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class ExerciseService : IExerciseService
{
    private readonly AppDbContext _dbContext;

    public ExerciseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<ExerciseDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var exercises = await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .Where(e => e.CreatedByDoctorId == null)
            .Include(e => e.CategoryLinks)
            .ThenInclude(link => link.Category)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<ExerciseDto>>.Success(
            exercises.Select(MapToDto).ToList());
    }

    public async Task<AuthResult<ExerciseDto>> GetByIdAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .AsNoTracking()
            .Include(e => e.CategoryLinks)
            .ThenInclude(link => link.Category)
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<ExerciseDto>.Failure(["Exercise not found."]);
        }

        return AuthResult<ExerciseDto>.Success(MapToDto(exercise));
    }

    public async Task<AuthResult<ExerciseDto>> CreateAsync(
        CreateExerciseDto request,
        CancellationToken cancellationToken = default)
    {
        var categoryIds = NormalizeCategoryIds(request.CategoryIds);
        var categoryResult = await ValidateCategoriesAsync(categoryIds, cancellationToken);
        if (!categoryResult.Succeeded)
        {
            return AuthResult<ExerciseDto>.Failure(categoryResult.Errors);
        }

        var exercise = new Exercise
        {
            ExerciseId = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Instructions = request.Instructions,
            VideoUrl = request.VideoUrl,
            MediaType = request.MediaType,
            Equipment = request.Equipment,
            Difficulty = request.Difficulty,
            CreatedByDoctorId = null,
        };

        foreach (var categoryId in categoryIds)
        {
            exercise.CategoryLinks.Add(new ExerciseCategoryLink
            {
                ExerciseId = exercise.ExerciseId,
                ExerciseCategoryId = categoryId,
            });
        }

        _dbContext.Exercises.Add(exercise);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(exercise.ExerciseId, cancellationToken);
    }

    public async Task<AuthResult<ExerciseDto>> UpdateAsync(
        Guid exerciseId,
        UpdateExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        var categoryIds = NormalizeCategoryIds(request.CategoryIds);
        var categoryResult = await ValidateCategoriesAsync(categoryIds, cancellationToken);
        if (!categoryResult.Succeeded)
        {
            return AuthResult<ExerciseDto>.Failure(categoryResult.Errors);
        }

        var exercise = await _dbContext.Exercises
            .Include(e => e.CategoryLinks)
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<ExerciseDto>.Failure(["Exercise not found."]);
        }

        exercise.Title = request.Title;
        exercise.Description = request.Description;
        exercise.Instructions = request.Instructions;
        exercise.VideoUrl = request.VideoUrl;
        exercise.MediaType = request.MediaType;
        exercise.Equipment = request.Equipment;
        exercise.Difficulty = request.Difficulty;

        _dbContext.ExerciseCategoryLinks.RemoveRange(exercise.CategoryLinks);
        exercise.CategoryLinks.Clear();

        foreach (var categoryId in categoryIds)
        {
            exercise.CategoryLinks.Add(new ExerciseCategoryLink
            {
                ExerciseId = exercise.ExerciseId,
                ExerciseCategoryId = categoryId,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(exercise.ExerciseId, cancellationToken);
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<bool>.Failure(["Exercise not found."]);
        }

        var assignments = await _dbContext.UserExercises
            .Where(ue => ue.ExerciseId == exerciseId)
            .ToListAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            assignment.IsActive = false;
            assignment.SoftDelete();
        }

        exercise.SoftDelete();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> ActivateAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<bool>.Failure(["Exercise not found."]);
        }

        if (exercise.IsEnabled)
        {
            return AuthResult<bool>.Failure(["Exercise is already active."]);
        }

        exercise.IsEnabled = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    internal static ExerciseDto MapToDto(Exercise exercise) =>
        new(
            exercise.ExerciseId,
            exercise.Title,
            exercise.Description,
            exercise.Instructions,
            exercise.VideoUrl,
            exercise.MediaType,
            exercise.Equipment,
            exercise.Difficulty,
            exercise.CreatedByDoctorId,
            exercise.CreatedAt,
            exercise.CategoryLinks
                .Where(link => link.Category is not null && link.Category.IsEnabled)
                .OrderBy(link => link.Category.SortOrder)
                .ThenBy(link => link.Category.NameEn)
                .Select(link => new ExerciseCategorySummaryDto(
                    link.ExerciseCategoryId,
                    link.Category.NameFa,
                    link.Category.NameEn))
                .ToList(),
            exercise.IsEnabled);

    private static List<Guid> NormalizeCategoryIds(IEnumerable<Guid>? categoryIds) =>
        (categoryIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    private async Task<AuthResult<bool>> ValidateCategoriesAsync(
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            return AuthResult<bool>.Success(true);
        }

        var existingCount = await _dbContext.ExerciseCategories
            .Where(category => categoryIds.Contains(category.ExerciseCategoryId))
            .CountAsync(cancellationToken);

        if (existingCount != categoryIds.Count)
        {
            return AuthResult<bool>.Failure(["One or more categories were not found."]);
        }

        return AuthResult<bool>.Success(true);
    }
}
