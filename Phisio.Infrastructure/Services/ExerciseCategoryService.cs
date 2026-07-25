using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.ExerciseCategories;
using Phisio.Application.Common;
using Phisio.Application.ExerciseCategories;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class ExerciseCategoryService : IExerciseCategoryService
{
    private readonly AppDbContext _dbContext;

    public ExerciseCategoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<ExerciseCategoryDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.ExerciseCategories
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.NameEn)
            .Select(category => MapToDto(category))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<ExerciseCategoryDto>>.Success(categories);
    }

    public async Task<AuthResult<ExerciseCategoryDto>> GetByIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ExerciseCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ExerciseCategoryId == categoryId, cancellationToken);

        if (category is null)
        {
            return AuthResult<ExerciseCategoryDto>.Failure(["Category not found."]);
        }

        return AuthResult<ExerciseCategoryDto>.Success(MapToDto(category));
    }

    public async Task<AuthResult<ExerciseCategoryDto>> CreateAsync(
        CreateExerciseCategoryDto request,
        CancellationToken cancellationToken = default)
    {
        var category = new ExerciseCategory
        {
            ExerciseCategoryId = Guid.NewGuid(),
            NameFa = request.NameFa.Trim(),
            NameEn = request.NameEn.Trim(),
            SortOrder = request.SortOrder,
        };

        _dbContext.ExerciseCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<ExerciseCategoryDto>.Success(MapToDto(category));
    }

    public async Task<AuthResult<ExerciseCategoryDto>> UpdateAsync(
        Guid categoryId,
        UpdateExerciseCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ExerciseCategories
            .FirstOrDefaultAsync(item => item.ExerciseCategoryId == categoryId, cancellationToken);

        if (category is null)
        {
            return AuthResult<ExerciseCategoryDto>.Failure(["Category not found."]);
        }

        category.NameFa = request.NameFa.Trim();
        category.NameEn = request.NameEn.Trim();
        category.SortOrder = request.SortOrder;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<ExerciseCategoryDto>.Success(MapToDto(category));
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ExerciseCategories
            .FirstOrDefaultAsync(item => item.ExerciseCategoryId == categoryId, cancellationToken);

        if (category is null)
        {
            return AuthResult<bool>.Failure(["Category not found."]);
        }

        category.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> ActivateAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ExerciseCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.ExerciseCategoryId == categoryId, cancellationToken);

        if (category is null)
        {
            return AuthResult<bool>.Failure(["Category not found."]);
        }

        if (category.IsEnabled)
        {
            return AuthResult<bool>.Failure(["Category is already active."]);
        }

        category.IsEnabled = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    private static ExerciseCategoryDto MapToDto(ExerciseCategory category) =>
        new(
            category.ExerciseCategoryId,
            category.NameFa,
            category.NameEn,
            category.SortOrder,
            category.CreatedAt,
            category.IsEnabled);
}
