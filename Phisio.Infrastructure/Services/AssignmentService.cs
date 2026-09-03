using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Assignments;
using Phisio.Application.Assignments;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.Relationships;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class AssignmentService : IAssignmentService
{
    private readonly AppDbContext _dbContext;
    private readonly ICareRelationshipService _careRelationships;

    public AssignmentService(
        AppDbContext dbContext,
        ICareRelationshipService careRelationships)
    {
        _dbContext = dbContext;
        _careRelationships = careRelationships;
    }

    /// <summary>
    /// Legacy assignment API. Assignments are clinic-scoped via UserExercise.ClinicId.
    /// </summary>
    public async Task<AuthResult<AssignmentDto>> CreateAsync(
        Guid doctorId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var clinicId = request.ClinicId;
        if (clinicId == Guid.Empty)
        {
            return AuthResult<AssignmentDto>.Failure([DoctorPatientErrors.ClinicRequired]);
        }

        var hasDoctorPatientLink = await _careRelationships.HasActiveRelationshipAsync(
            doctorId,
            request.PatientId,
            clinicId,
            cancellationToken);

        if (!hasDoctorPatientLink)
        {
            return AuthResult<AssignmentDto>.Failure(
                ["Patient not found or is not linked to this doctor."]);
        }

        var exercise = await _dbContext.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExerciseId == request.ExerciseId, cancellationToken);

        if (exercise is null)
        {
            return AuthResult<AssignmentDto>.Failure(["Exercise not found."]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existingActive = await _dbContext.UserExercises
            .FirstOrDefaultAsync(
                ue => ue.PatientId == request.PatientId
                    && ue.DoctorId == doctorId
                    && ue.ClinicId == clinicId
                    && ue.ExerciseId == request.ExerciseId
                    && ue.ScheduledDate == today
                    && ue.IsActive
                    && ue.IsEnabled,
                cancellationToken);

        if (existingActive is not null)
        {
            // Idempotent merge: keep a single row for this exercise/day.
            existingActive.AssignedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return AuthResult<AssignmentDto>.Success(MapToDto(existingActive, exercise.Title));
        }

        var assignment = UserExercise.CreateAdHoc(
            CareContext.From(doctorId, request.PatientId, clinicId),
            request.ExerciseId,
            today,
            DateTime.UtcNow);

        _dbContext.UserExercises.Add(assignment);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index race: reload the winner and treat as merged.
            var raced = await _dbContext.UserExercises
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    ue => ue.PatientId == request.PatientId
                        && ue.DoctorId == doctorId
                        && ue.ClinicId == clinicId
                        && ue.ExerciseId == request.ExerciseId
                        && ue.ScheduledDate == today
                        && ue.IsActive
                        && ue.IsEnabled,
                    cancellationToken);

            if (raced is null)
            {
                return AuthResult<AssignmentDto>.Failure(
                    ["Unable to assign exercise due to a concurrent update."]);
            }

            return AuthResult<AssignmentDto>.Success(MapToDto(raced, exercise.Title));
        }

        return AuthResult<AssignmentDto>.Success(MapToDto(assignment, exercise.Title));
    }

    public async Task<AuthResult<IReadOnlyList<AssignmentDto>>> GetByPatientIdAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        if (clinicId == Guid.Empty)
        {
            return AuthResult<IReadOnlyList<AssignmentDto>>.Failure([DoctorPatientErrors.ClinicRequired]);
        }

        var hasDoctorPatientLink = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .AnyAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (!hasDoctorPatientLink)
        {
            return AuthResult<IReadOnlyList<AssignmentDto>>.Failure(
                ["Patient not found or is not linked to this doctor."]);
        }

        var assignments = await _dbContext.UserExercises
            .AsNoTracking()
            .Where(ue => ue.DoctorId == doctorId && ue.PatientId == patientId && ue.ClinicId == clinicId)
            .Join(
                _dbContext.Exercises.AsNoTracking(),
                ue => ue.ExerciseId,
                e => e.ExerciseId,
                (ue, e) => new { Assignment = ue, e.Title })
            .OrderByDescending(x => x.Assignment.AssignedAt)
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<AssignmentDto>>.Success(
            assignments.Select(x => MapToDto(x.Assignment, x.Title)).ToList());
    }

    public async Task<AuthResult<IReadOnlyList<AssignmentDto>>> GetMyAssignmentsAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _dbContext.UserExercises
            .AsNoTracking()
            .Where(ue => ue.PatientId == patientId && ue.IsActive)
            .Join(
                _dbContext.Exercises.AsNoTracking(),
                ue => ue.ExerciseId,
                e => e.ExerciseId,
                (ue, e) => new { Assignment = ue, e.Title })
            .OrderByDescending(x => x.Assignment.AssignedAt)
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<AssignmentDto>>.Success(
            assignments.Select(x => MapToDto(x.Assignment, x.Title)).ToList());
    }

    public async Task<AuthResult<bool>> DeactivateAsync(
        Guid doctorId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.UserExercises
            .FirstOrDefaultAsync(ue => ue.UserExerciseId == assignmentId, cancellationToken);

        if (assignment is null || assignment.DoctorId != doctorId)
        {
            return AuthResult<bool>.Failure(["Assignment not found."]);
        }

        var hasClinicAccess = await _dbContext.DoctorPatients
            .WhereActive()
            .AnyAsync(
                dp => dp.DoctorId == doctorId
                    && dp.PatientId == assignment.PatientId
                    && dp.ClinicId == assignment.ClinicId,
                cancellationToken);

        if (!hasClinicAccess)
        {
            return AuthResult<bool>.Failure(["Assignment not found."]);
        }

        if (!assignment.IsActive)
        {
            return AuthResult<bool>.Failure(["Assignment is already inactive."]);
        }

        assignment.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<IReadOnlyList<AssignmentReportDto>>> GetReportAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            where ue.IsActive
            join patient in _dbContext.Users.AsNoTracking() on ue.PatientId equals patient.Id
            join doctor in _dbContext.Users.AsNoTracking() on ue.DoctorId equals doctor.Id
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            orderby patient.Name, doctor.Name, exercise.Title
            select new
            {
                PatientName = patient.Name,
                DoctorName = doctor.Name,
                ExerciseTitle = exercise.Title,
            })
            .ToListAsync(cancellationToken);

        var report = rows
            .GroupBy(row => new { row.PatientName, row.DoctorName })
            .Select(group => new AssignmentReportDto(
                group.Key.PatientName,
                group.Key.DoctorName,
                group.Select(row => row.ExerciseTitle).ToList()))
            .OrderBy(row => row.PatientName)
            .ThenBy(row => row.DoctorName)
            .ToList();

        return AuthResult<IReadOnlyList<AssignmentReportDto>>.Success(report);
    }

    private static AssignmentDto MapToDto(UserExercise assignment, string exerciseTitle) =>
        new(
            assignment.UserExerciseId,
            assignment.DoctorId,
            assignment.PatientId,
            assignment.ClinicId,
            assignment.ExerciseId,
            exerciseTitle,
            assignment.AssignedAt,
            assignment.IsActive);
}
