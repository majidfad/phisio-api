using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.Notifications;
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

    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notifications;

    public DoctorPatientService(
        AppDbContext dbContext,
        INotificationService? notifications = null)
    {
        _dbContext = dbContext;
        _notifications = notifications ?? NoOpNotificationService.Instance;
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
                    Relation = dp,
                })
            .Join(
                _dbContext.Clinics.AsNoTracking().Where(c => c.IsEnabled),
                item => item.Relation.ClinicId,
                clinic => clinic.ClinicId,
                (item, clinic) => new
                {
                    item.Patient,
                    item.Relation,
                    Clinic = clinic,
                })
            .OrderBy(x => x.Patient.Name)
            .ThenBy(x => x.Clinic.Name)
            .Select(x => new DoctorPatientDto(
                x.Patient.Id,
                x.Patient.Name,
                x.Patient.PhoneNumber ?? string.Empty,
                x.Relation.CreatedAt,
                x.Clinic.ClinicId,
                x.Clinic.Name))
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
                (dp, u) => new { Relation = dp, Patient = u })
            .Join(
                _dbContext.Clinics.AsNoTracking().Where(c => c.IsEnabled),
                item => item.Relation.ClinicId,
                clinic => clinic.ClinicId,
                (item, clinic) => new { item.Relation, item.Patient, Clinic = clinic })
            .OrderByDescending(x => x.Relation.CreatedAt)
            .Select(x => new DoctorPatientRequestDto(
                x.Patient.Id,
                x.Patient.Name,
                x.Patient.PhoneNumber ?? string.Empty,
                x.Relation.CreatedAt,
                x.Clinic.ClinicId,
                x.Clinic.Name))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorPatientRequestDto>>.Success(requests);
    }

    public async Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
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

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _notifications.NotifyAsync(
            patientId,
            NotificationType.LinkApproved,
            "Link approved",
            $"{doctorName} accepted your care request.",
            new { doctorId, doctorName },
            cancellationToken);

        var clinicName = await _dbContext.Clinics
            .AsNoTracking()
            .Where(clinic => clinic.ClinicId == clinicId)
            .Select(clinic => clinic.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? string.Empty;

        return AuthResult<DoctorPatientDto>.Success(new DoctorPatientDto(
            patient.Id,
            patient.Name,
            patient.PhoneNumber ?? string.Empty,
            relationship.CreatedAt,
            clinicId,
            clinicName));
    }

    public async Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([RequestNotFoundError]);
        }

        relationship.Status = DoctorPatientStatus.Rejected;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _notifications.NotifyAsync(
            patientId,
            NotificationType.LinkRejected,
            "Link declined",
            $"{doctorName} declined your care request.",
            new { doctorId, doctorName },
            cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WhereActive()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([RelationshipNotFoundError]);
        }

        relationship.IsEnabled = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _notifications.NotifyAsync(
            patientId,
            NotificationType.PatientRemoved,
            "Care link ended",
            $"{doctorName} removed you from their patient list.",
            new { doctorId, doctorName },
            cancellationToken);

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
                exercise.MediaType,
                ue.AssignedAt,
                ue.ScheduledDate,
                ue.Sets,
                ue.Reps,
                ue.ClinicianNote,
                ue.PatientCue))
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
        if (request.Items.Count == 0)
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

        var itemsByExerciseId = request.Items
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Last());
        var distinctExerciseIds = itemsByExerciseId.Keys.ToList();
        var distinctScheduledDates = request.ScheduledDates.Distinct().ToList();

        var validExerciseIds = await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .Where(exercise => distinctExerciseIds.Contains(exercise.ExerciseId))
            .Where(exercise =>
                exercise.CreatedByDoctorId == doctorId)
            .Select(exercise => exercise.ExerciseId)
            .ToListAsync(cancellationToken);

        if (validExerciseIds.Count == 0)
        {
            return AuthResult<AssignPatientExercisesResultDto>.Failure([NoValidExercisesError]);
        }

        // Merge into the patient's day schedule: same exercise+date updates
        // dosage in place; different exercises coexist on the same day.
        var existingActiveAssignments = await _dbContext.UserExercises
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
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
                    ApplyDosage(existingAssignment, dosage, assignedAt);
                }
                else if (inactiveByKey.TryGetValue(key, out var inactiveAssignment))
                {
                    inactiveAssignment.Reactivate(assignedAt);
                    ApplyDosage(inactiveAssignment, dosage, assignedAt);
                }
                else
                {
                    var assignment = new UserExercise
                    {
                        UserExerciseId = Guid.NewGuid(),
                        DoctorId = doctorId,
                        PatientId = patientId,
                        ExerciseId = exerciseId,
                        AssignedAt = assignedAt,
                        ScheduledDate = scheduledDate,
                        IsActive = true,
                        IsEnabled = true,
                    };
                    ApplyDosage(assignment, dosage, assignedAt);
                    _dbContext.UserExercises.Add(assignment);
                }

                assignedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (assignedCount > 0)
        {
            var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
            await _notifications.NotifyAsync(
                patientId,
                NotificationType.ExercisesAssigned,
                "New exercises assigned",
                assignedCount == 1
                    ? $"{doctorName} assigned 1 exercise for you."
                    : $"{doctorName} assigned {assignedCount} exercises for you.",
                new { doctorId, doctorName, count = assignedCount },
                cancellationToken);
        }

        return AuthResult<AssignPatientExercisesResultDto>.Success(new AssignPatientExercisesResultDto(assignedCount));
    }

    private static void ApplyDosage(
        UserExercise assignment,
        AssignPatientExerciseItem dosage,
        DateTime assignedAt)
    {
        assignment.ApplyLatestDosage(
            assignedAt,
            dosage.Sets,
            dosage.Reps,
            dosage.ClinicianNote,
            dosage.PatientCue);
    }

    public async Task<AuthResult<PatientExerciseHistoryResponse>> GetExerciseHistoryAsync(
        Guid doctorId,
        Guid patientId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignments = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            where ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
                && ue.ScheduledDate <= today
            select new AssignmentSnapshot(
                ue.UserExerciseId,
                ue.ExerciseId,
                exercise.Title,
                ue.ScheduledDate,
                ue.Sets,
                ue.Reps,
                ue.ClinicianNote,
                ue.PatientCue))
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
                    [],
                    0,
                    page,
                    pageSize));
        }

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

        var scheduledDates = assignments
            .Select(assignment => assignment.ScheduledDate)
            .Distinct()
            .OrderByDescending(date => date)
            .ToList();

        var completedDaysCount = scheduledDates.Count(date =>
            (completionsByDate.GetValueOrDefault(date) ?? []).Count > 0);
        var missedDaysCount = Math.Max(scheduledDates.Count - completedDaysCount, 0);
        var adherencePercentage = scheduledDates.Count == 0
            ? 0
            : (int)Math.Round(
                completedDaysCount * 100.0 / scheduledDates.Count,
                MidpointRounding.AwayFromZero);

        var pageDates = scheduledDates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dailyHistory = new List<PatientExerciseHistoryDayDto>(pageDates.Count);
        foreach (var date in pageDates)
        {
            var completedSet = completionsByDate.GetValueOrDefault(date) ?? [];
            var exercisesForDay = assignments
                .Where(assignment => assignment.ScheduledDate == date)
                .Select(assignment => new PatientExerciseHistoryExerciseDto(
                    assignment.UserExerciseId,
                    assignment.ExerciseId,
                    assignment.Title,
                    completedSet.Contains(assignment.UserExerciseId),
                    assignment.Sets,
                    assignment.Reps,
                    assignment.ClinicianNote,
                    assignment.PatientCue))
                .ToList();

            var completedCount = exercisesForDay.Count(exercise => exercise.IsCompleted);
            feedbackByDate.TryGetValue(date, out var feedback);
            dailyHistory.Add(new PatientExerciseHistoryDayDto(
                date,
                completedCount,
                completedCount > 0,
                exercisesForDay,
                feedback?.ImprovementScore,
                feedback?.HardnessScore,
                feedback?.Comment));
        }

        var summary = new PatientExerciseHistorySummaryDto(
            assignments.Count,
            completedDaysCount,
            missedDaysCount,
            adherencePercentage);

        return AuthResult<PatientExerciseHistoryResponse>.Success(
            new PatientExerciseHistoryResponse(
                patientDto,
                summary,
                dailyHistory,
                scheduledDates.Count,
                page,
                pageSize));
    }

    public async Task<AuthResult<DoctorPatientOverviewDto>> GetPatientOverviewAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patientInfo = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            join patient in _dbContext.Users.AsNoTracking() on dp.PatientId equals patient.Id
            where dp.DoctorId == doctorId && dp.PatientId == patientId && patient.IsEnabled
            select new
            {
                patient.Id,
                patient.Name,
                patient.PhoneNumber,
                LinkedAt = dp.CreatedAt,
                PatientRegisteredAt = patient.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (patientInfo is null)
        {
            return AuthResult<DoctorPatientOverviewDto>.Failure([PatientNotFoundError]);
        }

        var history = await GetExerciseHistoryAsync(doctorId, patientId, page: 1, pageSize: 1, cancellationToken);
        if (!history.Succeeded || history.Value is null)
        {
            return AuthResult<DoctorPatientOverviewDto>.Failure(history.Errors);
        }

        var programs = await GetPatientProgramsAsync(doctorId, patientId, cancellationToken);
        if (!programs.Succeeded || programs.Value is null)
        {
            return AuthResult<DoctorPatientOverviewDto>.Failure(programs.Errors);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeToday = await _dbContext.UserExercises
            .AsNoTracking()
            .CountAsync(
                ue => ue.DoctorId == doctorId
                    && ue.PatientId == patientId
                    && ue.IsActive
                    && ue.IsEnabled
                    && ue.ScheduledDate == today,
                cancellationToken);

        return AuthResult<DoctorPatientOverviewDto>.Success(
            new DoctorPatientOverviewDto(
                patientInfo.Id,
                patientInfo.Name,
                patientInfo.PhoneNumber ?? string.Empty,
                patientInfo.LinkedAt,
                patientInfo.PatientRegisteredAt,
                history.Value.Summary,
                programs.Value,
                activeToday));
    }

    public async Task<AuthResult<IReadOnlyList<ExerciseProgramDto>>> GetPatientProgramsAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var hasActiveRelationship = await _dbContext.DoctorPatients
            .WhereActive()
            .AnyAsync(dp => dp.DoctorId == doctorId && dp.PatientId == patientId, cancellationToken);

        if (!hasActiveRelationship)
        {
            return AuthResult<IReadOnlyList<ExerciseProgramDto>>.Failure([PatientNotFoundError]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var programs = await _dbContext.ExercisePrograms
            .AsNoTracking()
            .Where(p => p.DoctorId == doctorId && p.PatientId == patientId && p.IsEnabled)
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
        CancellationToken cancellationToken = default)
    {
        var hasActiveRelationship = await _dbContext.DoctorPatients
            .WhereActive()
            .AnyAsync(dp => dp.DoctorId == doctorId && dp.PatientId == patientId, cancellationToken);

        if (!hasActiveRelationship)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([PatientNotFoundError]);
        }

        var itemsByExerciseId = request.Items
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Last());
        var validExerciseIds = await GetValidExerciseIdsAsync(doctorId, itemsByExerciseId.Keys.ToList(), cancellationToken);
        if (validExerciseIds.Count == 0)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([NoValidExercisesError]);
        }

        // Only materialize assignments from today onward so past dates never
        // collide with historical assignments kept after a program is deleted.
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

        var programId = Guid.NewGuid();
        var program = new ExerciseProgram
        {
            ProgramId = programId,
            DoctorId = doctorId,
            PatientId = patientId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CadenceType = request.CadenceType,
            DaysOfWeekMask = request.DaysOfWeekMask,
            IntervalDays = request.IntervalDays,
            IsEnabled = true,
        };

        foreach (var exerciseId in validExerciseIds)
        {
            var dosage = itemsByExerciseId[exerciseId];
            _dbContext.ProgramExercises.Add(new ProgramExercise
            {
                ProgramExerciseId = Guid.NewGuid(),
                ProgramId = programId,
                ExerciseId = exerciseId,
                Sets = dosage.Sets,
                Reps = dosage.Reps,
                ClinicianNote = dosage.ClinicianNote,
                PatientCue = dosage.PatientCue,
                IsEnabled = true,
            });
        }

        _dbContext.ExercisePrograms.Add(program);

        var assignedCount = await MaterializeProgramAssignmentsAsync(
            doctorId,
            patientId,
            programId,
            scheduleDates,
            validExerciseIds,
            itemsByExerciseId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _notifications.NotifyAsync(
            patientId,
            NotificationType.ProgramCreated,
            "New exercise program",
            $"{doctorName} created an exercise program for you.",
            new { doctorId, doctorName, programId, count = assignedCount },
            cancellationToken);

        return AuthResult<CreateExerciseProgramResultDto>.Success(
            new CreateExerciseProgramResultDto(programId, assignedCount));
    }

    public async Task<AuthResult<CreateExerciseProgramResultDto>> UpdateProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        UpdateExerciseProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.ExercisePrograms
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(
                p => p.ProgramId == programId
                    && p.DoctorId == doctorId
                    && p.PatientId == patientId
                    && p.IsEnabled,
                cancellationToken);

        if (program is null)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([DoctorPatientErrors.ProgramNotFound]);
        }

        var itemsByExerciseId = request.Items
            .GroupBy(item => item.ExerciseId)
            .ToDictionary(group => group.Key, group => group.Last());
        var validExerciseIds = await GetValidExerciseIdsAsync(doctorId, itemsByExerciseId.Keys.ToList(), cancellationToken);
        if (validExerciseIds.Count == 0)
        {
            return AuthResult<CreateExerciseProgramResultDto>.Failure([NoValidExercisesError]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var regenerateFrom = today;

        var futureAssignments = await _dbContext.UserExercises
            .Where(ue =>
                ue.ProgramId == programId
                && ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.ScheduledDate >= regenerateFrom
                && ue.IsActive
                && ue.IsEnabled)
            .ToListAsync(cancellationToken);

        var futureIds = futureAssignments.Select(ue => ue.UserExerciseId).ToList();
        var completedTodayIds = await _dbContext.ExerciseCompletions
            .AsNoTracking()
            .Where(c =>
                c.IsEnabled
                && c.CompletionDate >= regenerateFrom
                && futureIds.Contains(c.UserExerciseId))
            .Select(c => c.UserExerciseId)
            .ToListAsync(cancellationToken);
        var completedSet = completedTodayIds.ToHashSet();

        foreach (var assignment in futureAssignments)
        {
            if (completedSet.Contains(assignment.UserExerciseId))
            {
                continue;
            }

            assignment.IsActive = false;
            assignment.IsEnabled = false;
        }

        foreach (var programExercise in program.Exercises.Where(e => e.IsEnabled))
        {
            programExercise.IsEnabled = false;
        }

        program.StartDate = request.StartDate;
        program.EndDate = request.EndDate;
        program.CadenceType = request.CadenceType;
        program.DaysOfWeekMask = request.DaysOfWeekMask;
        program.IntervalDays = request.IntervalDays;

        foreach (var exerciseId in validExerciseIds)
        {
            var dosage = itemsByExerciseId[exerciseId];
            _dbContext.ProgramExercises.Add(new ProgramExercise
            {
                ProgramExerciseId = Guid.NewGuid(),
                ProgramId = programId,
                ExerciseId = exerciseId,
                Sets = dosage.Sets,
                Reps = dosage.Reps,
                ClinicianNote = dosage.ClinicianNote,
                PatientCue = dosage.PatientCue,
                IsEnabled = true,
            });
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

        var assignedCount = await MaterializeProgramAssignmentsAsync(
            doctorId,
            patientId,
            programId,
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
        CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.ExercisePrograms
            .Include(p => p.Exercises)
            .FirstOrDefaultAsync(
                p => p.ProgramId == programId
                    && p.DoctorId == doctorId
                    && p.PatientId == patientId
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
                && ue.ScheduledDate >= today
                && ue.IsActive
                && ue.IsEnabled)
            .ToListAsync(cancellationToken);

        var futureIds = futureAssignments.Select(ue => ue.UserExerciseId).ToList();
        var completedIds = futureIds.Count == 0
            ? []
            : await _dbContext.ExerciseCompletions
                .AsNoTracking()
                .Where(c =>
                    c.IsEnabled
                    && c.CompletionDate >= today
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

            assignment.IsActive = false;
            assignment.IsEnabled = false;
        }

        SoftDeleteExtensions.SoftDeleteRange(program.Exercises.Where(e => e.IsEnabled));
        program.SoftDelete();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<PatientExerciseStatsResponse>> GetExerciseStatsAsync(
        Guid doctorId,
        Guid patientId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var hasActiveRelationship = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .AnyAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (!hasActiveRelationship)
        {
            return AuthResult<PatientExerciseStatsResponse>.Failure([PatientNotFoundError]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeTo = to ?? today;
        var rangeFrom = from ?? rangeTo.AddDays(-29);
        if (rangeFrom > rangeTo)
        {
            (rangeFrom, rangeTo) = (rangeTo, rangeFrom);
        }

        var assignments = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            where ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.IsActive
                && ue.IsEnabled
                && exercise.IsEnabled
                && ue.ScheduledDate >= rangeFrom
                && ue.ScheduledDate <= rangeTo
            select new
            {
                ue.UserExerciseId,
                ue.ExerciseId,
                Title = exercise.Title,
                ue.ScheduledDate,
            })
            .ToListAsync(cancellationToken);

        var userExerciseIds = assignments.Select(a => a.UserExerciseId).ToList();
        var completedIds = userExerciseIds.Count == 0
            ? new HashSet<Guid>()
            : (await _dbContext.ExerciseCompletions
                .AsNoTracking()
                .Where(c =>
                    c.IsEnabled
                    && c.DoctorId == doctorId
                    && c.PatientId == patientId
                    && userExerciseIds.Contains(c.UserExerciseId)
                    && c.CompletionDate >= rangeFrom
                    && c.CompletionDate <= rangeTo)
                .Select(c => c.UserExerciseId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var feedbackByDate = await _dbContext.DailyPatientFeedbacks
            .AsNoTracking()
            .Where(f =>
                f.PatientId == patientId
                && f.DoctorId == doctorId
                && f.IsEnabled
                && f.FeedbackDate >= rangeFrom
                && f.FeedbackDate <= rangeTo)
            .ToDictionaryAsync(f => f.FeedbackDate, cancellationToken);

        var scheduledDates = assignments
            .Select(a => a.ScheduledDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var daily = new List<PatientExerciseStatsDailyDto>(scheduledDates.Count);
        foreach (var date in scheduledDates)
        {
            var dayAssignments = assignments.Where(a => a.ScheduledDate == date).ToList();
            var completedCount = dayAssignments.Count(a => completedIds.Contains(a.UserExerciseId));
            feedbackByDate.TryGetValue(date, out var feedback);
            daily.Add(new PatientExerciseStatsDailyDto(
                date,
                dayAssignments.Count,
                completedCount,
                completedCount > 0,
                feedback?.ImprovementScore,
                feedback?.HardnessScore));
        }

        var completedDays = daily.Count(d => d.IsCompleted);
        var missedDays = Math.Max(daily.Count - completedDays, 0);
        var assignedExerciseCount = assignments.Count;
        var completedExerciseCount = assignments.Count(a => completedIds.Contains(a.UserExerciseId));
        var adherencePercentage = daily.Count == 0
            ? 0
            : (int)Math.Round(completedDays * 100.0 / daily.Count, MidpointRounding.AwayFromZero);
        var exerciseCompletionPercentage = assignedExerciseCount == 0
            ? 0
            : (int)Math.Round(
                completedExerciseCount * 100.0 / assignedExerciseCount,
                MidpointRounding.AwayFromZero);

        var improvementScores = daily
            .Where(d => d.ImprovementScore.HasValue)
            .Select(d => d.ImprovementScore!.Value)
            .ToList();
        var hardnessScores = daily
            .Where(d => d.HardnessScore.HasValue)
            .Select(d => d.HardnessScore!.Value)
            .ToList();

        var summary = new PatientExerciseStatsSummaryDto(
            daily.Count,
            completedDays,
            missedDays,
            adherencePercentage,
            assignedExerciseCount,
            completedExerciseCount,
            exerciseCompletionPercentage,
            improvementScores.Count == 0 ? null : Math.Round(improvementScores.Average(), 1),
            hardnessScores.Count == 0 ? null : Math.Round(hardnessScores.Average(), 1),
            improvementScores.Count);

        var weekly = daily
            .GroupBy(d => StartOfWeek(d.Date))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var weekCompleted = g.Count(d => d.IsCompleted);
                var weekScheduled = g.Count();
                var weekAdherence = weekScheduled == 0
                    ? 0
                    : (int)Math.Round(
                        weekCompleted * 100.0 / weekScheduled,
                        MidpointRounding.AwayFromZero);
                return new PatientExerciseStatsWeeklyDto(
                    g.Key,
                    weekScheduled,
                    weekCompleted,
                    weekAdherence);
            })
            .ToList();

        var exercises = assignments
            .GroupBy(a => new { a.ExerciseId, a.Title })
            .Select(g =>
            {
                var assigned = g.Count();
                var completed = g.Count(a => completedIds.Contains(a.UserExerciseId));
                var pct = assigned == 0
                    ? 0
                    : (int)Math.Round(completed * 100.0 / assigned, MidpointRounding.AwayFromZero);
                return new PatientExerciseStatsExerciseDto(
                    g.Key.ExerciseId,
                    g.Key.Title,
                    assigned,
                    completed,
                    pct);
            })
            .OrderBy(e => e.CompletionPercentage)
            .ThenBy(e => e.Title)
            .ToList();

        return AuthResult<PatientExerciseStatsResponse>.Success(
            new PatientExerciseStatsResponse(
                rangeFrom,
                rangeTo,
                summary,
                daily,
                weekly,
                exercises));
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday as week start
        return date.AddDays(-offset);
    }

    private async Task<List<Guid>> GetValidExerciseIdsAsync(
        Guid doctorId,
        IReadOnlyList<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Exercises
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled: true)
            .Where(exercise => exerciseIds.Contains(exercise.ExerciseId))
            .Where(exercise =>
                exercise.CreatedByDoctorId == doctorId)
            .Select(exercise => exercise.ExerciseId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Materializes program dates into the patient schedule by merging:
    /// same exercise+date updates dosage (latest wins); different exercises coexist.
    /// Completed leftovers are retired so a fresh row is created.
    /// </summary>
    private async Task<int> MaterializeProgramAssignmentsAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        IReadOnlyList<DateOnly> scheduleDates,
        IReadOnlyList<Guid> validExerciseIds,
        IReadOnlyDictionary<Guid, AssignPatientExerciseItem> itemsByExerciseId,
        CancellationToken cancellationToken)
    {
        if (scheduleDates.Count == 0 || validExerciseIds.Count == 0)
        {
            return 0;
        }

        var existingActive = await _dbContext.UserExercises
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && assignment.IsActive
                && assignment.IsEnabled
                && validExerciseIds.Contains(assignment.ExerciseId)
                && scheduleDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        var inactiveAssignments = await _dbContext.UserExercises
            .IgnoreQueryFilters()
            .Where(assignment =>
                assignment.DoctorId == doctorId
                && assignment.PatientId == patientId
                && (!assignment.IsActive || !assignment.IsEnabled)
                && validExerciseIds.Contains(assignment.ExerciseId)
                && scheduleDates.Contains(assignment.ScheduledDate))
            .ToListAsync(cancellationToken);

        // Never reuse an assignment that already has a completion for its scheduled
        // date — otherwise a new program would inherit "done" from a prior plan.
        var candidateIds = existingActive
            .Concat(inactiveAssignments)
            .Select(assignment => assignment.UserExerciseId)
            .Distinct()
            .ToList();
        var completedAssignmentIds = candidateIds.Count == 0
            ? []
            : (await _dbContext.ExerciseCompletions
                .AsNoTracking()
                .Where(completion =>
                    completion.IsEnabled
                    && candidateIds.Contains(completion.UserExerciseId)
                    && scheduleDates.Contains(completion.CompletionDate))
                .Select(completion => new { completion.UserExerciseId, completion.CompletionDate })
                .ToListAsync(cancellationToken))
                .Where(completion => existingActive
                        .Concat(inactiveAssignments)
                        .Any(assignment =>
                            assignment.UserExerciseId == completion.UserExerciseId
                            && assignment.ScheduledDate == completion.CompletionDate))
                .Select(completion => completion.UserExerciseId)
                .ToHashSet();

        foreach (var completed in existingActive.Where(a => completedAssignmentIds.Contains(a.UserExerciseId)))
        {
            completed.Retire();
        }

        var existingByKey = existingActive
            .Where(assignment => !completedAssignmentIds.Contains(assignment.UserExerciseId))
            .GroupBy(assignment => (assignment.ExerciseId, assignment.ScheduledDate))
            .ToDictionary(group => group.Key, group => group.First());

        var inactiveByKey = inactiveAssignments
            .Where(assignment => !completedAssignmentIds.Contains(assignment.UserExerciseId))
            .GroupBy(assignment => (assignment.ExerciseId, assignment.ScheduledDate))
            .ToDictionary(group => group.Key, group => group.First());

        var assignedAt = DateTime.UtcNow;
        var assignedCount = 0;

        foreach (var scheduledDate in scheduleDates)
        {
            foreach (var exerciseId in validExerciseIds)
            {
                var key = (exerciseId, scheduledDate);
                var dosage = itemsByExerciseId[exerciseId];

                if (existingByKey.TryGetValue(key, out var existingAssignment))
                {
                    // Merge into the consolidated day schedule (latest dosage wins).
                    existingAssignment.ProgramId = programId;
                    ApplyDosage(existingAssignment, dosage, assignedAt);
                    continue;
                }

                if (inactiveByKey.TryGetValue(key, out var inactiveAssignment))
                {
                    inactiveAssignment.Reactivate(assignedAt, programId);
                    ApplyDosage(inactiveAssignment, dosage, assignedAt);
                }
                else
                {
                    var assignment = new UserExercise
                    {
                        UserExerciseId = Guid.NewGuid(),
                        DoctorId = doctorId,
                        PatientId = patientId,
                        ExerciseId = exerciseId,
                        AssignedAt = assignedAt,
                        ScheduledDate = scheduledDate,
                        ProgramId = programId,
                        IsActive = true,
                        IsEnabled = true,
                    };
                    ApplyDosage(assignment, dosage, assignedAt);
                    _dbContext.UserExercises.Add(assignment);
                }

                assignedCount++;
            }
        }

        return assignedCount;
    }

    private static ExerciseProgramDto MapProgramDto(ExerciseProgram program, int upcoming, int past) =>
        new(
            program.ProgramId,
            program.PatientId,
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

    private sealed record AssignmentSnapshot(
        Guid UserExerciseId,
        Guid ExerciseId,
        string Title,
        DateOnly ScheduledDate,
        int? Sets,
        string? Reps,
        string? ClinicianNote,
        string? PatientCue);

    private async Task<string> GetUserNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken)
        ?? "User";
}
