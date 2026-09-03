using Microsoft.EntityFrameworkCore;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services.Care;

internal static class CareExerciseCatalog
{
    internal static async Task<List<Guid>> GetValidExerciseIdsAsync(
        AppDbContext dbContext,
        Guid doctorId,
        IReadOnlyList<Guid> exerciseIds,
        CancellationToken cancellationToken) =>
        await dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .Where(exercise => exerciseIds.Contains(exercise.ExerciseId))
            .Where(exercise => exercise.CreatedByDoctorId == doctorId)
            .Select(exercise => exercise.ExerciseId)
            .ToListAsync(cancellationToken);

    internal static async Task<string> GetUserNameAsync(
        AppDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Name)
            .FirstOrDefaultAsync(cancellationToken)
        ?? "User";
}
