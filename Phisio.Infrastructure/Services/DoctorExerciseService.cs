using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class DoctorExerciseService : IDoctorExerciseService
{
    private readonly AppDbContext _dbContext;

    public DoctorExerciseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorExerciseDto>>> GetExercisesAsync(
        CancellationToken cancellationToken = default)
    {
        var exercises = await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new DoctorExerciseDto(
                e.ExerciseId,
                e.Title,
                string.IsNullOrWhiteSpace(e.Description) ? null : e.Description,
                e.VideoUrl))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorExerciseDto>>.Success(exercises);
    }
}
