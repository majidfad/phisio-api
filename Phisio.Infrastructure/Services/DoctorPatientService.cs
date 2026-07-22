using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class DoctorPatientService : IDoctorPatientService
{
    public const string PatientNotFoundError = DoctorPatientErrors.PatientNotFound;
    public const string RelationshipNotFoundError = DoctorPatientErrors.RelationshipNotFound;
    public const string RequestNotFoundError = DoctorPatientErrors.RequestNotFound;
    public const string NoExercisesSelectedError = DoctorPatientErrors.NoExercisesSelected;
    public const string NoDatesSelectedError = DoctorPatientErrors.NoDatesSelected;
    public const string NoValidExercisesError = DoctorPatientErrors.NoValidExercises;
    public const string DuplicateExerciseAssignmentError = DoctorPatientErrors.DuplicateAssignment;

    private readonly AppDbContext _dbContext;

    public DoctorPatientService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patients = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(dp => dp.DoctorId == doctorId)
            .Join(
                _dbContext.Users
                    .AsNoTracking()
                    .Where(u =>
                        u.Role == UserRole.Patient &&
                        u.IsEnabled),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new
                {
                    Patient = u,
                    Relation = dp
                })
            .OrderBy(x => x.Patient.Name)
            .Select(x => new DoctorPatientDto(
                x.Patient.Id,
                x.Patient.Name,
                x.Patient.PhoneNumber ?? string.Empty,
                x.Relation.CreatedAt))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorPatientDto>>.Success(patients);
    }

    public async Task<AuthResult<IReadOnlyList<DoctorPatientRequestDto>>> GetPendingRequestsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WherePending()
            .Where(dp => dp.DoctorId == doctorId)
            .Join(
                _dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.Role == UserRole.Patient && u.IsEnabled),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new DoctorPatientRequestDto(
                    u.Id,
                    u.Name,
                    u.PhoneNumber ?? string.Empty,
                    dp.CreatedAt))
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorPatientRequestDto>>.Success(requests);
    }

    public async Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<DoctorPatientDto>.Failure([RequestNotFoundError]);
        }

        var patient = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == patientId && u.Role == UserRole.Patient && u.IsEnabled,
                cancellationToken);

        if (patient is null)
        {
            return AuthResult<DoctorPatientDto>.Failure([PatientNotFoundError]);
        }

        relationship.Status = DoctorPatientStatus.Approved;
        relationship.CreatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<DoctorPatientDto>.Success(new DoctorPatientDto(
            patient.Id,
            patient.Name,
            patient.PhoneNumber ?? string.Empty,
            relationship.CreatedAt));
    }

    public async Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([RequestNotFoundError]);
        }

        relationship.Status = DoctorPatientStatus.Rejected;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WhereActive()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([RelationshipNotFoundError]);
        }

        relationship.IsEnabled = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>> GetPatientExercisesAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var exercises = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            join ue in _dbContext.UserExercises.AsNoTracking()
                on new { dp.DoctorId, dp.PatientId } equals new { DoctorId = ue.DoctorId, PatientId = ue.PatientId }
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            where dp.DoctorId == doctorId
                && dp.PatientId == patientId
                && ue.IsActive
            orderby ue.ScheduledDate descending, ue.AssignedAt descending
            select new DoctorPatientExerciseDto(
                ue.UserExerciseId,
                exercise.ExerciseId,
                exercise.Title,
                exercise.VideoUrl,
                ue.AssignedAt,
                ue.ScheduledDate))
            .ToListAsync(cancellationToken);

        if (exercises.Count > 0)
        {
            return AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Success(exercises);
        }

        var hasActiveRelationship = await _dbContext.DoctorPatients
            .WhereActive()
            .AnyAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (!hasActiveRelationship)
        {
            return AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Failure([PatientNotFoundError]);
        }

        return AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Success(exercises);
    }

    public async Task<AuthResult<AssignPatientExercisesResultDto>> AssignExercisesAsync(
        Guid doctorId,
        Guid patientId,
        AssignPatientExercisesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExerciseIds.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([NoExercisesSelectedError]);
        }

        if (request.ScheduledDates.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([NoDatesSelectedError]);
        }

        var hasActiveRelationship = await _dbContext.DoctorPatients
            .WhereActive()
            .AnyAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (!hasActiveRelationship)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([PatientNotFoundError]);
        }

        var distinctExerciseIds = request.ExerciseIds.Distinct().ToList();
        var distinctScheduledDates = request.ScheduledDates.Distinct().ToList();

        var validExerciseIds = await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .Where(exercise => distinctExerciseIds.Contains(exercise.ExerciseId))
            .Select(exercise => exercise.ExerciseId)
            .ToListAsync(cancellationToken);

        if (validExerciseIds.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([NoValidExercisesError]);
        }

        var existingActiveAssignments = await _dbContext.UserExercises
            .AsNoTracking()
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && assignment.IsActive
                && assignment.IsEnabled
                && validExerciseIds.Contains(assignment.ExerciseId)
                && distinctScheduledDates.Contains(assignment.ScheduledDate))
            .Select(assignment => new { assignment.ExerciseId, assignment.ScheduledDate })
            .ToListAsync(cancellationToken);

        if (existingActiveAssignments.Count > 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([DuplicateExerciseAssignmentError]);
        }

        var inactiveAssignments = await _dbContext.UserExercises
            .IgnoreQueryFilters()
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && (!assignment.IsActive || !assignment.IsEnabled)
                && validExerciseIds.Contains(assignment.ExerciseId)
                && distinctScheduledDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        var inactiveByKey = inactiveAssignments.ToDictionary(
            assignment => (assignment.ExerciseId, assignment.ScheduledDate));
        var assignedAt = DateTime.UtcNow;
        var assignedCount = 0;

        foreach (var scheduledDate in distinctScheduledDates)
        {
            foreach (var exerciseId in validExerciseIds)
            {
                var key = (exerciseId, scheduledDate);

                if (inactiveByKey.TryGetValue(key, out var inactiveAssignment))
                {
                    inactiveAssignment.IsActive = true;
                    inactiveAssignment.IsEnabled = true;
                    inactiveAssignment.AssignedAt = assignedAt;
                }
                else
                {
                    _dbContext.UserExercises.Add(new UserExercise
                    {
                        UserExerciseId = Guid.NewGuid(),
                        DoctorId = doctorId,
                        PatientId = patientId,
                        ExerciseId = exerciseId,
                        AssignedAt = assignedAt,
                        ScheduledDate = scheduledDate,
                        IsActive = true,
                        IsEnabled = true,
                    });
                }

                assignedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<AssignPatientExercisesResultDto>.Success(new AssignPatientExercisesResultDto(assignedCount));
    }

    public async Task<AuthResult<PatientExerciseHistoryResponse>> GetExerciseHistoryAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patientInfo = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            join patient in _dbContext.Users.AsNoTracking()
                on dp.PatientId equals patient.Id
            where dp.DoctorId == doctorId
                && dp.PatientId == patientId
                && patient.IsEnabled
            select new
            {
                patient.Id,
                patient.Name,
                patient.PhoneNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (patientInfo is null)
        {
            return AuthResult<PatientExerciseHistoryResponse>.Failure([PatientNotFoundError]);
        }

        var assignments = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            where ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
            select new AssignmentSnapshot(
                ue.UserExerciseId,
                ue.ExerciseId,
                exercise.Title,
                ue.AssignedAt))
            .ToListAsync(cancellationToken);

        var patientDto = new PatientExerciseHistoryPatientDto(
            patientInfo.Id,
            patientInfo.Name,
            patientInfo.PhoneNumber ?? string.Empty);

        if (assignments.Count == 0)
        {
            return AuthResult<PatientExerciseHistoryResponse>.Success(
                new PatientExerciseHistoryResponse(
                    patientDto,
                    new PatientExerciseHistorySummaryDto(0, 0, 0, 0),
                    []));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstAssignmentDate = assignments
            .Min(assignment => DateOnly.FromDateTime(assignment.AssignedAt));
        var assignedDays = today.DayNumber - firstAssignmentDate.DayNumber + 1;
        var userExerciseIds = assignments
            .Select(assignment => assignment.UserExerciseId)
            .ToList();

        var completionEntries = await _dbContext.ExerciseCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.DoctorId == doctorId
                && completion.PatientId == patientId
                && completion.IsEnabled
                && userExerciseIds.Contains(completion.UserExerciseId))
            .Select(completion => new
            {
                completion.CompletionDate,
                completion.UserExerciseId,
            })
            .ToListAsync(cancellationToken);

        var completionsByDate = completionEntries
            .GroupBy(entry => entry.CompletionDate)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.UserExerciseId).ToHashSet());

        var feedbackByDate = await _dbContext.DailyPatientFeedbacks
            .AsNoTracking()
            .Where(feedback =>
                feedback.PatientId == patientId
                && feedback.DoctorId == doctorId
                && feedback.IsEnabled)
            .ToDictionaryAsync(feedback => feedback.FeedbackDate, cancellationToken);

        var completedDaysCount = completionsByDate.Count;
        var missedDaysCount = Math.Max(assignedDays - completedDaysCount, 0);
        var adherencePercentage = assignedDays == 0
            ? 0
            : (int)Math.Round(completedDaysCount * 100.0 / assignedDays, MidpointRounding.AwayFromZero);

        var dailyHistory = new List<PatientExerciseHistoryDayDto>();
        for (var date = today; date >= firstAssignmentDate; date = date.AddDays(-1))
        {
            var completedSet = completionsByDate.GetValueOrDefault(date) ?? [];
            var exercisesForDay = assignments
                .Where(assignment => DateOnly.FromDateTime(assignment.AssignedAt) <= date)
                .Select(assignment => new PatientExerciseHistoryExerciseDto(
                    assignment.UserExerciseId,
                    assignment.ExerciseId,
                    assignment.Title,
                    completedSet.Contains(assignment.UserExerciseId)))
                .ToList();

            var completedCount = exercisesForDay.Count(exercise => exercise.IsCompleted);
            feedbackByDate.TryGetValue(date, out var feedback);
            dailyHistory.Add(new PatientExerciseHistoryDayDto(
                date,
                completedCount,
                completedCount > 0,
                exercisesForDay,
                feedback?.ImprovementScore,
                feedback?.Comment));
        }

        var summary = new PatientExerciseHistorySummaryDto(
            assignments.Count,
            completedDaysCount,
            missedDaysCount,
            adherencePercentage);

        return AuthResult<PatientExerciseHistoryResponse>.Success(
            new PatientExerciseHistoryResponse(patientDto, summary, dailyHistory));
    }

    private sealed record AssignmentSnapshot(
        Guid UserExerciseId,
        Guid ExerciseId,
        string Title,
        DateTime AssignedAt);
}
