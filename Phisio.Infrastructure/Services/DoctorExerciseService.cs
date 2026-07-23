using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;
using Phisio.Application.Exercises;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class DoctorExerciseService : IDoctorExerciseService
{
    private readonly AppDbContext _dbContext;

    public DoctorExerciseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorExerciseDto>>> GetLibraryAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var exercises = await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .Where(e => e.CreatedByDoctorId == doctorId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => MapToDto(e, doctorId))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorExerciseDto>>.Success(exercises);
    }

    public async Task<AuthResult<IReadOnlyList<ExerciseDto>>> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var exercises = await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .Where(e => e.CreatedByDoctorId == null)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => MapCatalogDto(e))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<ExerciseDto>>.Success(exercises);
    }

    public async Task<AuthResult<DoctorExerciseDto>> CreateAsync(
        Guid doctorId,
        CreateDoctorExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        var exercise = new Exercise
        {
            ExerciseId = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Instructions = request.Instructions,
            VideoUrl = request.VideoUrl,
            MediaType = request.MediaType,
            BodyRegion = request.BodyRegion,
            Equipment = request.Equipment,
            Difficulty = request.Difficulty,
            CreatedByDoctorId = doctorId,
        };

        _dbContext.Exercises.Add(exercise);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<DoctorExerciseDto>.Success(MapToDto(exercise, doctorId));
    }

    public async Task<AuthResult<DoctorExerciseDto>> UpdateAsync(
        Guid doctorId,
        Guid exerciseId,
        UpdateDoctorExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<DoctorExerciseDto>.Failure(["Exercise not found."]);
        }

        if (exercise.CreatedByDoctorId != doctorId)
        {
            return AuthResult<DoctorExerciseDto>.Failure(["You can only edit your own exercises."]);
        }

        exercise.Title = request.Title;
        exercise.Description = request.Description;
        exercise.Instructions = request.Instructions;
        exercise.VideoUrl = request.VideoUrl;
        exercise.MediaType = request.MediaType;
        exercise.BodyRegion = request.BodyRegion;
        exercise.Equipment = request.Equipment;
        exercise.Difficulty = request.Difficulty;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<DoctorExerciseDto>.Success(MapToDto(exercise, doctorId));
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        Guid doctorId,
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _dbContext.Exercises
            .FirstOrDefaultAsync(e => e.ExerciseId == exerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<bool>.Failure(["Exercise not found."]);
        }

        if (exercise.CreatedByDoctorId != doctorId)
        {
            return AuthResult<bool>.Failure(["You can only archive your own exercises."]);
        }

        exercise.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    private static DoctorExerciseDto MapToDto(Exercise exercise, Guid doctorId) =>
        new(
            exercise.ExerciseId,
            exercise.Title,
            string.IsNullOrWhiteSpace(exercise.Description) ? null : exercise.Description,
            exercise.Instructions,
            exercise.VideoUrl,
            exercise.MediaType,
            exercise.BodyRegion,
            exercise.Equipment,
            exercise.Difficulty,
            exercise.CreatedByDoctorId,
            exercise.CreatedByDoctorId == doctorId,
            exercise.CreatedAt);

    private static ExerciseDto MapCatalogDto(Exercise exercise) =>
        new(
            exercise.ExerciseId,
            exercise.Title,
            exercise.Description,
            exercise.Instructions,
            exercise.VideoUrl,
            exercise.MediaType,
            exercise.BodyRegion,
            exercise.Equipment,
            exercise.Difficulty,
            exercise.CreatedByDoctorId,
            exercise.CreatedAt,
            exercise.IsEnabled);
}
