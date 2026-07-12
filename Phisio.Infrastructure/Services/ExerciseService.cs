using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
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
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => MapToDto(e))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<ExerciseDto>>.Success(exercises);
    }

    public async Task<AuthResult<ExerciseDto>> GetByIdAsync(
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .AsNoTracking()
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
        var exercise = new Exercise
        {
            ExerciseId = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            VideoUrl = request.VideoUrl,
        };

        _dbContext.Exercises.Add(exercise);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<ExerciseDto>.Success(MapToDto(exercise));
    }

    public async Task<AuthResult<ExerciseDto>> UpdateAsync(
        Guid exerciseId,
        UpdateExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<ExerciseDto>.Failure(["Exercise not found."]);
        }

        exercise.Title = request.Title;
        exercise.Description = request.Description;
        exercise.VideoUrl = request.VideoUrl;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<ExerciseDto>.Success(MapToDto(exercise));
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

        SoftDeleteExtensions.SoftDeleteRange(assignments);
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

    private static ExerciseDto MapToDto(Exercise exercise) =>
        new(
            exercise.ExerciseId,
            exercise.Title,
            exercise.Description,
            exercise.VideoUrl,
            exercise.CreatedAt,
            exercise.IsEnabled);
}
