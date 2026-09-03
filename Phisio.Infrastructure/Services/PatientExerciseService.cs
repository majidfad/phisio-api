using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.PatientExercises;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Domain.Events;
using Phisio.Infrastructure.Events;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services.Care;

namespace Phisio.Infrastructure.Services;

public class PatientExerciseService : IPatientExerciseService
{
    private readonly AppDbContext _dbContext;
    private readonly IDomainEventDispatcher _domainEvents;

    public PatientExerciseService(
        AppDbContext dbContext,
        IDomainEventDispatcher? domainEvents = null)
    {
        _dbContext = dbContext;
        _domainEvents = domainEvents ?? NoOpDomainEventDispatcher.Instance;
    }

    public async Task<AuthResult<PatientExercisesResponse>> GetExercisesAsync(
        Guid patientId,
        DateOnly? scheduledDate = null,
        Guid? doctorId = null,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var doctorName = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join doctor in _dbContext.Users.AsNoTracking() on ue.DoctorId equals doctor.Id
            join relationship in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
                on new { ue.DoctorId, ue.PatientId, ue.ClinicId }
                equals new { relationship.DoctorId, relationship.PatientId, relationship.ClinicId }
            where ue.PatientId == patientId
                && ue.IsActive
                && ue.IsEnabled
                && (doctorId == null || ue.DoctorId == doctorId)
                && (clinicId == null || ue.ClinicId == clinicId)
            orderby ue.AssignedAt descending
            select doctor.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var exercises = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            join clinic in _dbContext.Clinics.AsNoTracking() on ue.ClinicId equals clinic.ClinicId
            join relationship in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
                on new { ue.DoctorId, ue.PatientId, ue.ClinicId }
                equals new { relationship.DoctorId, relationship.PatientId, relationship.ClinicId }
            join completion in _dbContext.ExerciseCompletions.AsNoTracking()
                on new { ue.UserExerciseId, CompletionDate = today } equals new { completion.UserExerciseId, completion.CompletionDate }
                into completions
            from completion in completions.DefaultIfEmpty()
            where ue.PatientId == patientId
                && (doctorId == null || ue.DoctorId == doctorId)
                && (clinicId == null || ue.ClinicId == clinicId)
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
                && clinic.IsEnabled
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
                ue.PatientCue,
                clinic.ClinicId,
                clinic.Name))
            .ToListAsync(cancellationToken);

        return AuthResult<PatientExercisesResponse>.Success(
            new PatientExercisesResponse(doctorName, exercises));
    }

    public async Task<AuthResult<PatientTodayExercisesResponse>> GetTodayExercisesAsync(
        Guid patientId,
        Guid? doctorId = null,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var exerciseRows = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            join doctor in _dbContext.Users.AsNoTracking() on ue.DoctorId equals doctor.Id
            join clinic in _dbContext.Clinics.AsNoTracking() on ue.ClinicId equals clinic.ClinicId
            join relationship in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
                on new { ue.DoctorId, ue.PatientId, ue.ClinicId }
                equals new { relationship.DoctorId, relationship.PatientId, relationship.ClinicId }
            join completion in _dbContext.ExerciseCompletions.AsNoTracking()
                on new { ue.UserExerciseId, CompletionDate = today } equals new { completion.UserExerciseId, completion.CompletionDate }
                into completions
            from completion in completions.DefaultIfEmpty()
            where ue.PatientId == patientId
                && (doctorId == null || ue.DoctorId == doctorId)
                && (clinicId == null || ue.ClinicId == clinicId)
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
                && clinic.IsEnabled
                && ue.ScheduledDate == today
            orderby doctor.Name, clinic.Name, exercise.Title
            select new
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                ClinicId = clinic.ClinicId,
                ClinicName = clinic.Name,
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
                    ue.PatientCue,
                    clinic.ClinicId,
                    clinic.Name),
            })
            .ToListAsync(cancellationToken);

        var doctorGroups = exerciseRows
            .GroupBy(row => new { row.DoctorId, row.DoctorName, row.ClinicId, row.ClinicName })
            .OrderBy(group => group.Key.DoctorName)
            .ThenBy(group => group.Key.ClinicName)
            .Select(group => new PatientDoctorExerciseGroupDto(
                group.Key.DoctorId,
                group.Key.DoctorName,
                group.Key.ClinicId,
                group.Key.ClinicName,
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

        var careKeys = assignments
            .Select(assignment => (assignment.DoctorId, assignment.ClinicId))
            .Distinct()
            .ToList();

        var activeRelationships = await _dbContext.DoctorPatients
            .WhereActive()
            .Where(dp => dp.PatientId == patientId)
            .Select(dp => new { dp.DoctorId, dp.ClinicId })
            .ToListAsync(cancellationToken);

        var activeKeys = activeRelationships
            .Select(relationship => (relationship.DoctorId, relationship.ClinicId))
            .ToHashSet();

        if (careKeys.Any(key => !activeKeys.Contains(key)))
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

            var patientName = await CareExerciseCatalog.GetUserNameAsync(_dbContext, patientId, cancellationToken);

            var byDoctor = assignments
                .Where(a => createdIds.Contains(a.UserExerciseId))
                .GroupBy(a => a.DoctorId)
                .Select(g => new { DoctorId = g.Key, Count = g.Count() });

            foreach (var group in byDoctor)
            {
                await _domainEvents.DispatchAsync(
                    new ExercisesCompletedEvent(
                        group.DoctorId,
                        patientId,
                        patientName,
                        group.Count,
                        DateTime.UtcNow),
                    cancellationToken);
            }
        }

        return AuthResult<CompleteExercisesResponse>.Success(
            new CompleteExercisesResponse(today, createdIds, skippedIds));
    }
}
