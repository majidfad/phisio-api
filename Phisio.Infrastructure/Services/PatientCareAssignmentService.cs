using Microsoft.EntityFrameworkCore;
using Phisio.Application.CareDelivery;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.Relationships;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Events;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services.Care;

namespace Phisio.Infrastructure.Services;

public class PatientCareAssignmentService : IPatientCareAssignmentService
{
    private readonly AppDbContext _dbContext;
    private readonly ICareRelationshipService _careRelationships;
    private readonly IDomainEventDispatcher _domainEvents;

    public PatientCareAssignmentService(
        AppDbContext dbContext,
        ICareRelationshipService careRelationships,
        IDomainEventDispatcher domainEvents)
    {
        _dbContext = dbContext;
        _careRelationships = careRelationships;
        _domainEvents = domainEvents;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>> GetPatientExercisesAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Failure(access.Errors);
        }

        var exercises = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            where ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.ClinicId == clinicId
                && ue.IsActive
            orderby ue.ScheduledDate descending, ue.AssignedAt descending
            select new DoctorPatientExerciseDto(
                ue.UserExerciseId,
                exercise.ExerciseId,
                exercise.Title,
                exercise.VideoUrl,
                exercise.MediaType,
                ue.AssignedAt,
                ue.ScheduledDate,
                ue.Sets,
                ue.Reps,
                ue.ClinicianNote,
                ue.PatientCue))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Success(exercises);
    }

    public async Task<AuthResult<AssignPatientExercisesResultDto>> AssignExercisesAsync(
        Guid doctorId,
        Guid patientId,
        AssignPatientExercisesRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([DoctorPatientErrors.NoExercisesSelected]);
        }

        if (request.ScheduledDates.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([DoctorPatientErrors.NoDatesSelected]);
        }

        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure(access.Errors);
        }

        var itemsByExerciseId = request.Items
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Last());
        var distinctExerciseIds = itemsByExerciseId.Keys.ToList();
        var distinctScheduledDates = request.ScheduledDates.Distinct().ToList();

        var validExerciseIds = await CareExerciseCatalog.GetValidExerciseIdsAsync(
            _dbContext,
            doctorId,
            distinctExerciseIds,
            cancellationToken);

        if (validExerciseIds.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([DoctorPatientErrors.NoValidExercises]);
        }

        var existingActiveAssignments = await _dbContext.UserExercises
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && assignment.ClinicId == clinicId
                && assignment.IsActive
                && assignment.IsEnabled
                && validExerciseIds.Contains(assignment.ExerciseId)
                && distinctScheduledDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        var existingByKey = existingActiveAssignments
            .GroupBy(assignment => (assignment.ExerciseId, assignment.ScheduledDate))
            .ToDictionary(group => group.Key, group => group.First());

        var inactiveAssignments = await _dbContext.UserExercises
            .IgnoreQueryFilters()
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && assignment.ClinicId == clinicId
                && (!assignment.IsActive || !assignment.IsEnabled)
                && validExerciseIds.Contains(assignment.ExerciseId)
                && distinctScheduledDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        var inactiveByKey = inactiveAssignments
            .GroupBy(assignment => (assignment.ExerciseId, assignment.ScheduledDate))
            .ToDictionary(group => group.Key, group => group.First());
        var assignedAt = DateTime.UtcNow;
        var assignedCount = 0;

        foreach (var scheduledDate in distinctScheduledDates)
        {
            foreach (var exerciseId in validExerciseIds)
            {
                var dosage = itemsByExerciseId[exerciseId];
                var key = (exerciseId, scheduledDate);

                if (existingByKey.TryGetValue(key, out var existingAssignment))
                {
                    PatientAssignmentMaterializer.ApplyDosage(existingAssignment, dosage, assignedAt);
                }
                else if (inactiveByKey.TryGetValue(key, out var inactiveAssignment))
                {
                    inactiveAssignment.Reactivate(assignedAt);
                    PatientAssignmentMaterializer.ApplyDosage(inactiveAssignment, dosage, assignedAt);
                }
                else
                {
                    var assignment = UserExercise.CreateAdHoc(
                        CareContext.From(doctorId, patientId, clinicId),
                        exerciseId,
                        scheduledDate,
                        assignedAt);
                    PatientAssignmentMaterializer.ApplyDosage(assignment, dosage, assignedAt);
                    _dbContext.UserExercises.Add(assignment);
                }

                assignedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (assignedCount > 0)
        {
            var doctorName = await CareExerciseCatalog.GetUserNameAsync(_dbContext, doctorId, cancellationToken);
            await _domainEvents.DispatchAsync(
                new ExercisesAssignedEvent(
                    doctorId,
                    patientId,
                    clinicId,
                    doctorName,
                    assignedCount,
                    DateTime.UtcNow),
                cancellationToken);
        }

        return AuthResult<AssignPatientExercisesResultDto>.Success(new AssignPatientExercisesResultDto(assignedCount));
    }
}
