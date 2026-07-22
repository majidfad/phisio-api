using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.PatientExercises;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PatientExerciseService : IPatientExerciseService
{
    private readonly AppDbContext _dbContext;

    public PatientExerciseService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<PatientExercisesResponse>> GetExercisesAsync(
        Guid patientId,
        DateOnly? scheduledDate = null,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var doctorName = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            join doctor in _dbContext.Users.AsNoTracking() on dp.DoctorId equals doctor.Id
            where dp.PatientId == patientId
                && (doctorId == null || dp.DoctorId == doctorId)
            orderby dp.CreatedAt descending
            select doctor.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var exercises = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            join ue in _dbContext.UserExercises.AsNoTracking()
                on new { dp.DoctorId, dp.PatientId } equals new { DoctorId = ue.DoctorId, PatientId = ue.PatientId }
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            join completion in _dbContext.ExerciseCompletions.AsNoTracking()
                on new { ue.UserExerciseId, CompletionDate = today } equals new { completion.UserExerciseId, completion.CompletionDate }
                into completions
            from completion in completions.DefaultIfEmpty()
            where dp.PatientId == patientId
                && (doctorId == null || dp.DoctorId == doctorId)
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
                && (scheduledDate == null || ue.ScheduledDate == scheduledDate)
            orderby ue.ScheduledDate descending, ue.AssignedAt descending
            select new PatientExerciseItemDto(
                ue.UserExerciseId,
                exercise.ExerciseId,
                exercise.Title,
                exercise.VideoUrl,
                exercise.MediaType,
                exercise.Instructions,
                ue.AssignedAt,
                ue.ScheduledDate,
                completion != null,
                ue.Sets,
                ue.Reps,
                ue.HoldSeconds,
                ue.RestSeconds,
                ue.Side,
                ue.PatientCue))
            .ToListAsync(cancellationToken);

        if (doctorName is null && exercises.Count > 0)
        {
            doctorName = await (
                from ue in _dbContext.UserExercises.AsNoTracking()
                join doctor in _dbContext.Users.AsNoTracking() on ue.DoctorId equals doctor.Id
                where ue.PatientId == patientId
                    && (doctorId == null || ue.DoctorId == doctorId)
                    && ue.IsActive
                    && ue.IsEnabled
                orderby ue.AssignedAt descending
                select doctor.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return AuthResult<PatientExercisesResponse>.Success(
            new PatientExercisesResponse(doctorName, exercises));
    }

    public async Task<AuthResult<PatientTodayExercisesResponse>> GetTodayExercisesAsync(
        Guid patientId,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var exerciseRows = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            join ue in _dbContext.UserExercises.AsNoTracking()
                on new { dp.DoctorId, dp.PatientId } equals new { DoctorId = ue.DoctorId, PatientId = ue.PatientId }
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            join doctor in _dbContext.Users.AsNoTracking() on ue.DoctorId equals doctor.Id
            join completion in _dbContext.ExerciseCompletions.AsNoTracking()
                on new { ue.UserExerciseId, CompletionDate = today } equals new { completion.UserExerciseId, completion.CompletionDate }
                into completions
            from completion in completions.DefaultIfEmpty()
            where dp.PatientId == patientId
                && (doctorId == null || dp.DoctorId == doctorId)
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
                && ue.ScheduledDate == today
            orderby doctor.Name, exercise.Title
            select new
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Item = new PatientTodayExerciseItemDto(
                    ue.UserExerciseId,
                    exercise.ExerciseId,
                    exercise.Title,
                    exercise.VideoUrl,
                    exercise.MediaType,
                    exercise.Instructions,
                    ue.ScheduledDate,
                    completion != null,
                    ue.Sets,
                    ue.Reps,
                    ue.HoldSeconds,
                    ue.RestSeconds,
                    ue.Side,
                    ue.PatientCue),
            })
            .ToListAsync(cancellationToken);

        var doctorGroups = exerciseRows
            .GroupBy(row => new { row.DoctorId, row.DoctorName })
            .OrderBy(group => group.Key.DoctorName)
            .Select(group => new PatientDoctorExerciseGroupDto(
                group.Key.DoctorId,
                group.Key.DoctorName,
                group.Select(row => row.Item).ToList()))
            .ToList();

        return AuthResult<PatientTodayExercisesResponse>.Success(
            new PatientTodayExercisesResponse(doctorGroups));
    }

    public async Task<AuthResult<CompleteExercisesResponse>> CompleteExercisesAsync(
        Guid patientId,
        CompleteExercisesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userExerciseIds = request.UserExerciseIds.Distinct().ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (userExerciseIds.Count == 0)
        {
            return AuthResult<CompleteExercisesResponse>.Success(
                new CompleteExercisesResponse(today, [], []));
        }

        var assignments = await _dbContext.UserExercises
            .Where(ue => userExerciseIds.Contains(ue.UserExerciseId)
                && ue.PatientId == patientId
                && ue.IsActive
                && ue.IsEnabled)
            .ToListAsync(cancellationToken);

        if (assignments.Count != userExerciseIds.Count)
        {
            return AuthResult<CompleteExercisesResponse>.Failure([PatientExerciseErrors.AssignmentNotFound]);
        }

        var activeDoctorIds = await _dbContext.DoctorPatients
            .WhereActive()
            .Where(dp => dp.PatientId == patientId)
            .Select(dp => dp.DoctorId)
            .ToListAsync(cancellationToken);

        if (assignments.Any(assignment => !activeDoctorIds.Contains(assignment.DoctorId)))
        {
            return AuthResult<CompleteExercisesResponse>.Failure([PatientExerciseErrors.AssignmentNotFound]);
        }

        var assignmentIdList = assignments.Select(assignment => assignment.UserExerciseId).ToList();
        var existingCompletionIds = await _dbContext.ExerciseCompletions
            .IgnoreQueryFilters()
            .Where(completion =>
                assignmentIdList.Contains(completion.UserExerciseId)
                && completion.CompletionDate == today)
            .Select(completion => completion.UserExerciseId)
            .ToListAsync(cancellationToken);

        var existingIds = existingCompletionIds.ToHashSet();
        var createdIds = new List<Guid>();
        var skippedIds = new List<Guid>();

        foreach (var assignment in assignments)
        {
            if (existingIds.Contains(assignment.UserExerciseId))
            {
                skippedIds.Add(assignment.UserExerciseId);
                continue;
            }

            _dbContext.ExerciseCompletions.Add(new ExerciseCompletion
            {
                ExerciseCompletionId = Guid.NewGuid(),
                UserExerciseId = assignment.UserExerciseId,
                PatientId = assignment.PatientId,
                DoctorId = assignment.DoctorId,
                ExerciseId = assignment.ExerciseId,
                CompletionDate = today,
                IsEnabled = true,
            });

            createdIds.Add(assignment.UserExerciseId);
            existingIds.Add(assignment.UserExerciseId);
        }

        if (createdIds.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return AuthResult<CompleteExercisesResponse>.Success(
            new CompleteExercisesResponse(today, createdIds, skippedIds));
    }
}
