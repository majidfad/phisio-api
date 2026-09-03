using Microsoft.EntityFrameworkCore;
using Phisio.Application.CarePlans;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.ReadModels;
using Phisio.Application.Relationships;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PatientCareQueryService : IPatientCareQueryService
{
    private readonly AppDbContext _dbContext;
    private readonly ICareRelationshipService _careRelationships;
    private readonly IExerciseProgramService _programs;

    public PatientCareQueryService(
        AppDbContext dbContext,
        ICareRelationshipService careRelationships,
        IExerciseProgramService programs)
    {
        _dbContext = dbContext;
        _careRelationships = careRelationships;
        _programs = programs;
    }

    public async Task<AuthResult<PatientExerciseHistoryResponse>> GetExerciseHistoryAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<PatientExerciseHistoryResponse>.Failure(access.Errors);
        }

        var patientInfo = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive().WhereClinic(clinicId)
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
            return AuthResult<PatientExerciseHistoryResponse>.Failure([DoctorPatientErrors.PatientNotFound]);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignments = await (
            from ue in _dbContext.UserExercises.AsNoTracking()
            join exercise in _dbContext.Exercises.AsNoTracking() on ue.ExerciseId equals exercise.ExerciseId
            where ue.DoctorId == doctorId
                && ue.PatientId == patientId
                && ue.ClinicId == clinicId
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
                && feedback.ClinicId == clinicId
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
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<DoctorPatientOverviewDto>.Failure(access.Errors);
        }

        var patientInfo = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive().WhereClinic(clinicId)
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
            return AuthResult<DoctorPatientOverviewDto>.Failure([DoctorPatientErrors.PatientNotFound]);
        }

        var history = await GetExerciseHistoryAsync(
            doctorId,
            patientId,
            clinicId,
            page: 1,
            pageSize: 1,
            cancellationToken);
        if (!history.Succeeded || history.Value is null)
        {
            return AuthResult<DoctorPatientOverviewDto>.Failure(history.Errors);
        }

        var programs = await _programs.GetPatientProgramsAsync(doctorId, patientId, clinicId, cancellationToken);
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
                    && ue.ClinicId == clinicId
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

    public async Task<AuthResult<PatientExerciseStatsResponse>> GetExerciseStatsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var access = await _careRelationships.EnsureCareAccessAsync(doctorId, patientId, clinicId, cancellationToken);
        if (!access.Succeeded)
        {
            return AuthResult<PatientExerciseStatsResponse>.Failure(access.Errors);
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
                && ue.ClinicId == clinicId
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
                && f.ClinicId == clinicId
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
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private sealed record AssignmentSnapshot(
        Guid UserExerciseId,
        Guid ExerciseId,
        string Title,
        DateOnly ScheduledDate,
        int? Sets,
        string? Reps,
        string? ClinicianNote,
        string? PatientCue);
}
