using Microsoft.EntityFrameworkCore;
using Phisio.Application.CarePlans;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.Relationships;
using Phisio.Domain.CarePlans;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Events;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services.Care;

namespace Phisio.Infrastructure.Services;

public class ExerciseProgramService : IExerciseProgramService
{
    private readonly AppDbContext _dbContext;
    private readonly ICareRelationshipService _careRelationships;
    private readonly IDomainEventDispatcher _domainEvents;

    public ExerciseProgramService(
        AppDbContext dbContext,
        ICareRelationshipService careRelationships,
        IDomainEventDispatcher domainEvents)
    {
        _dbContext = dbContext;
        _careRelationships = careRelationships;
        _domainEvents = domainEvents;
    }

    public async Task<AuthResult<IReadOnlyList<ExerciseProgramDto>>> GetPatientProgramsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<IReadOnlyList<ExerciseProgramDto>>.Failure(access.Errors);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var programs = await _dbContext.ExercisePrograms
            .AsNoTracking()
            .Where(p =>
                p.DoctorId == doctorId
                && p.PatientId == patientId
                && p.ClinicId == clinicId
                && p.IsEnabled)
            .Include(p => p.Exercises.Where(e => e.IsEnabled))
            .ThenInclude(e => e.Exercise)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var programIds = programs.Select(p => p.ProgramId).ToList();
        var assignmentCounts = await _dbContext.UserExercises
            .AsNoTracking()
            .Where(ue =>
                ue.ProgramId != null
                && programIds.Contains(ue.ProgramId.Value)
                && ue.IsActive
                && ue.IsEnabled)
            .GroupBy(ue => ue.ProgramId!.Value)
            .Select(group => new
            {
                ProgramId = group.Key,
                Upcoming = group.Count(ue => ue.ScheduledDate >= today),
                Past = group.Count(ue => ue.ScheduledDate < today),
            })
            .ToListAsync(cancellationToken);

        var countsByProgram = assignmentCounts.ToDictionary(x => x.ProgramId);

        var dtos = programs.Select(program =>
        {
            countsByProgram.TryGetValue(program.ProgramId, out var counts);
            return MapProgramDto(program, counts?.Upcoming ?? 0, counts?.Past ?? 0);
        }).ToList();

        return AuthResult<IReadOnlyList<ExerciseProgramDto>>.Success(dtos);
    }

    public async Task<AuthResult<CreateExerciseProgramResultDto>> CreateProgramAsync(
        Guid doctorId,
        Guid patientId,
        CreateExerciseProgramRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure(access.Errors);
        }

        var itemsByExerciseId = request.Items
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Last());
        var validExerciseIds = await CareExerciseCatalog.GetValidExerciseIdsAsync(
            _dbContext,
            doctorId,
            itemsByExerciseId.Keys.ToList(),
            cancellationToken);
        if (validExerciseIds.Count == 0)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([DoctorPatientErrors.NoValidExercises]);
        }

        var scheduleDates = ExerciseProgramSchedule.ExpandFrom(
            request.StartDate,
            request.EndDate,
            DateOnly.FromDateTime(DateTime.UtcNow),
            request.CadenceType,
            request.DaysOfWeekMask,
            request.IntervalDays);

        if (scheduleDates.Count == 0)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([DoctorPatientErrors.NoScheduleDates]);
        }

        var context = CareContext.From(doctorId, patientId, clinicId);
        var program = ExerciseProgram.Create(
            context,
            request.StartDate,
            request.EndDate,
            request.CadenceType,
            request.DaysOfWeekMask,
            request.IntervalDays);

        var programExercises = program.ReplaceExercises(validExerciseIds.Select(exerciseId =>
        {
            var dosage = itemsByExerciseId[exerciseId];
            return new ProgramExerciseDosage(
                exerciseId,
                dosage.Sets,
                dosage.Reps,
                dosage.ClinicianNote,
                dosage.PatientCue);
        }));

        _dbContext.ExercisePrograms.Add(program);
        foreach (var programExercise in programExercises)
        {
            _dbContext.ProgramExercises.Add(programExercise);
        }

        var assignedCount = await PatientAssignmentMaterializer.MaterializeProgramAssignmentsAsync(
            _dbContext,
            program,
            scheduleDates,
            validExerciseIds,
            itemsByExerciseId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await CareExerciseCatalog.GetUserNameAsync(_dbContext, doctorId, cancellationToken);
        await _domainEvents.DispatchAsync(
            new ExerciseProgramCreatedEvent(
                doctorId,
                patientId,
                clinicId,
                program.ProgramId,
                doctorName,
                assignedCount,
                DateTime.UtcNow),
            cancellationToken);

        return AuthResult<CreateExerciseProgramResultDto>.Success(
            new CreateExerciseProgramResultDto(program.ProgramId, assignedCount));
    }

    public async Task<AuthResult<CreateExerciseProgramResultDto>> UpdateProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        UpdateExerciseProgramRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure(access.Errors);
        }

        var program = await _dbContext.ExercisePrograms
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(
                p => p.ProgramId == programId
                    && p.DoctorId == doctorId
                    && p.PatientId == patientId
                    && p.ClinicId == clinicId
                    && p.IsEnabled,
                cancellationToken);

        if (program is null)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([DoctorPatientErrors.ProgramNotFound]);
        }

        var itemsByExerciseId = request.Items
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Last());
        var validExerciseIds = await CareExerciseCatalog.GetValidExerciseIdsAsync(
            _dbContext,
            doctorId,
            itemsByExerciseId.Keys.ToList(),
            cancellationToken);
        if (validExerciseIds.Count == 0)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([DoctorPatientErrors.NoValidExercises]);
        }

        var regenerateFrom = DateOnly.FromDateTime(DateTime.UtcNow);

        var futureAssignments = await _dbContext.UserExercises
            .Where(ue =>
                ue.ProgramId == programId
                && ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.ClinicId == clinicId
                && ue.ScheduledDate >= regenerateFrom
                && ue.IsActive
                && ue.IsEnabled)
            .ToListAsync(cancellationToken);

        await RetireUncompletedFutureAssignmentsAsync(
            _dbContext,
            futureAssignments,
            regenerateFrom,
            cancellationToken);

        program.UpdateSchedule(
            request.StartDate,
            request.EndDate,
            request.CadenceType,
            request.DaysOfWeekMask,
            request.IntervalDays);

        var programExercises = program.ReplaceExercises(validExerciseIds.Select(exerciseId =>
        {
            var dosage = itemsByExerciseId[exerciseId];
            return new ProgramExerciseDosage(
                exerciseId,
                dosage.Sets,
                dosage.Reps,
                dosage.ClinicianNote,
                dosage.PatientCue);
        }));

        foreach (var programExercise in programExercises)
        {
            _dbContext.ProgramExercises.Add(programExercise);
        }

        var scheduleDates = ExerciseProgramSchedule.ExpandFrom(
            request.StartDate,
            request.EndDate,
            regenerateFrom,
            request.CadenceType,
            request.DaysOfWeekMask,
            request.IntervalDays);

        if (scheduleDates.Count == 0 && request.EndDate >= regenerateFrom)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([DoctorPatientErrors.NoScheduleDates]);
        }

        var assignedCount = await PatientAssignmentMaterializer.MaterializeProgramAssignmentsAsync(
            _dbContext,
            program,
            scheduleDates,
            validExerciseIds,
            itemsByExerciseId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AuthResult<CreateExerciseProgramResultDto>.Success(
            new CreateExerciseProgramResultDto(programId, assignedCount));
    }

    public async Task<AuthResult<bool>> DeleteProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<bool>.Failure(access.Errors);
        }

        var program = await _dbContext.ExercisePrograms
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(
                p => p.ProgramId == programId
                    && p.DoctorId == doctorId
                    && p.PatientId == patientId
                    && p.ClinicId == clinicId
                    && p.IsEnabled,
                cancellationToken);

        if (program is null)
        {
            return AuthResult<bool>.Failure([DoctorPatientErrors.ProgramNotFound]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var futureAssignments = await _dbContext.UserExercises
            .Where(ue =>
                ue.ProgramId == programId
                && ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.ClinicId == clinicId
                && ue.ScheduledDate >= today
                && ue.IsActive
                && ue.IsEnabled)
            .ToListAsync(cancellationToken);

        await RetireUncompletedFutureAssignmentsAsync(
            _dbContext,
            futureAssignments,
            today,
            cancellationToken);

        program.SoftDeleteWithExercises();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AuthResult<bool>.Success(true);
    }

    private static async Task RetireUncompletedFutureAssignmentsAsync(
        AppDbContext dbContext,
        IReadOnlyList<UserExercise> futureAssignments,
        DateOnly fromDate,
        CancellationToken cancellationToken)
    {
        var futureIds = futureAssignments.Select(ue => ue.UserExerciseId).ToList();
        var completedIds = futureIds.Count == 0
            ? []
            : await dbContext.ExerciseCompletions
                .AsNoTracking()
                .Where(c =>
                    c.IsEnabled
                    && c.CompletionDate >= fromDate
                    && futureIds.Contains(c.UserExerciseId))
                .Select(c => c.UserExerciseId)
                .ToListAsync(cancellationToken);
        var completedSet = completedIds.ToHashSet();

        foreach (var assignment in futureAssignments)
        {
            if (completedSet.Contains(assignment.UserExerciseId))
            {
                continue;
            }

            assignment.Retire();
        }
    }

    private static ExerciseProgramDto MapProgramDto(ExerciseProgram program, int upcoming, int past) =>
        new(
            program.ProgramId,
            program.PatientId,
            program.ClinicId,
            program.StartDate,
            program.EndDate,
            program.CadenceType,
            program.DaysOfWeekMask,
            program.IntervalDays,
            program.CreatedAt,
            program.Exercises
                .Where(e => e.IsEnabled)
                .Select(e => new ExerciseProgramExerciseDto(
                    e.ExerciseId,
                    e.Exercise?.Title ?? string.Empty,
                    e.Sets,
                    e.Reps,
                    e.ClinicianNote,
                    e.PatientCue))
                .ToList(),
            upcoming,
            past);
}
