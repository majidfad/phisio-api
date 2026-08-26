using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phisio.Application.Notifications;
using Phisio.Application.PatientSettings;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;

namespace Phisio.Infrastructure.Background;

/// <summary>
/// Creates exercise reminders per patient schedule (repeat mode, times, optional follow-up).
/// </summary>
public sealed class ExerciseReminderBackgroundService : BackgroundService
{
    private const string PrimarySlot = "primary";
    private const string FollowUpSlot = "followUp";

    /// <summary>
    /// Reminder eligibility is re-checked every minute so PreferredReminderTime is honored
    /// within ~1 minute (not up to 15 minutes later).
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExerciseReminderBackgroundService> _logger;

    public ExerciseReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExerciseReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short initial delay so the host can finish startup, then poll every minute.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exercise reminder job failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var utcNow = DateTime.UtcNow;
        var utcToday = DateOnly.FromDateTime(utcNow);
        var scheduleFrom = utcToday.AddDays(-1);
        var scheduleTo = utcToday.AddDays(1);

        var assignments = await dbContext.UserExercises
            .AsNoTracking()
            .Where(ue =>
                ue.IsActive
                && ue.IsEnabled
                && ue.ScheduledDate >= scheduleFrom
                && ue.ScheduledDate <= scheduleTo)
            .Select(ue => new { ue.PatientId, ue.UserExerciseId, ue.ScheduledDate })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            _logger.LogDebug(
                "Exercise reminder tick at {UtcNow:o}: no active assignments in {From}..{To}.",
                utcNow,
                scheduleFrom,
                scheduleTo);
            return;
        }

        var patientIds = assignments.Select(a => a.PatientId).Distinct().ToList();

        var patients = await dbContext.Users
            .AsNoTracking()
            .Where(u =>
                patientIds.Contains(u.Id)
                && u.Role == UserRole.Patient
                && u.IsEnabled
                && u.ExerciseRemindersEnabled)
            .Select(u => new
            {
                u.Id,
                u.PreferredReminderTime,
                u.TimeZoneId,
                u.ReminderRepeatMode,
                u.ReminderDaysOfWeekMask,
                u.ReminderIntervalDays,
                u.ReminderAnchorDate,
                u.ReminderFollowUpEnabled,
                u.ReminderFollowUpTime,
            })
            .ToListAsync(cancellationToken);

        if (patients.Count == 0)
        {
            _logger.LogDebug(
                "Exercise reminder tick at {UtcNow:o}: {AssignmentCount} assignments but no eligible patients with reminders enabled.",
                utcNow,
                assignments.Count);
            return;
        }

        var completions = await dbContext.ExerciseCompletions
            .AsNoTracking()
            .Where(c =>
                c.IsEnabled
                && c.CompletionDate >= scheduleFrom
                && c.CompletionDate <= scheduleTo)
            .Select(c => new { c.UserExerciseId, c.CompletionDate })
            .ToListAsync(cancellationToken);

        var completedSet = completions
            .Select(c => (c.UserExerciseId, c.CompletionDate))
            .ToHashSet();

        var recentReminders = await dbContext.Notifications
            .AsNoTracking()
            .Where(n =>
                patientIds.Contains(n.UserId)
                && n.Type == NotificationType.ExerciseReminder
                && n.CreatedAt >= utcNow.AddDays(-2))
            .Select(n => new { n.UserId, n.CreatedAt, n.Data })
            .ToListAsync(cancellationToken);

        var requests = new List<CreateNotificationRequest>();

        foreach (var patient in patients)
        {
            var timeZone = PatientSettingsService.ResolveTimeZone(patient.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            var localToday = DateOnly.FromDateTime(localNow);
            var localTime = TimeOnly.FromDateTime(localNow);

            if (!ReminderSchedule.IsReminderDay(
                    localToday,
                    patient.ReminderRepeatMode,
                    patient.ReminderDaysOfWeekMask == 0
                        ? ReminderSchedule.AllDaysMask
                        : patient.ReminderDaysOfWeekMask,
                    patient.ReminderIntervalDays,
                    patient.ReminderAnchorDate))
            {
                _logger.LogDebug(
                    "Skip patient {PatientId}: {LocalToday} is not a reminder day (mode {Mode}).",
                    patient.Id,
                    localToday,
                    patient.ReminderRepeatMode);
                continue;
            }

            // Assignments / completions are dated with UTC calendar days elsewhere in the app
            // (PatientExerciseService, AssignmentService). Match that here so "today's"
            // incomplete exercises are the same set the patient sees in the app.
            // Prefer localToday when it equals utcToday (normal daytime); when they differ
            // near midnight, still count either date so a pending row is not missed.
            var pendingCount = assignments.Count(a =>
                a.PatientId == patient.Id
                && (a.ScheduledDate == localToday || a.ScheduledDate == utcToday)
                && !completedSet.Contains((a.UserExerciseId, a.ScheduledDate)));

            if (pendingCount == 0)
            {
                _logger.LogInformation(
                    "Skip patient {PatientId} at local {LocalTime} (tz {TimeZone}): no incomplete exercises for local {LocalToday} / utc {UtcToday}. PreferredReminderTime={Preferred}.",
                    patient.Id,
                    localTime,
                    timeZone.Id,
                    localToday,
                    utcToday,
                    patient.PreferredReminderTime);
                continue;
            }

            var patientReminders = recentReminders
                .Where(n => n.UserId == patient.Id)
                .ToList();

            var hasPrimary = patientReminders.Any(n =>
                HasSlotForDate(
                    n.Data,
                    n.CreatedAt,
                    localToday,
                    timeZone,
                    PrimarySlot,
                    patient.PreferredReminderTime));
            var hasFollowUp = patientReminders.Any(n =>
                HasSlotForDate(
                    n.Data,
                    n.CreatedAt,
                    localToday,
                    timeZone,
                    FollowUpSlot,
                    patient.ReminderFollowUpTime));

            if (!hasPrimary && localTime >= patient.PreferredReminderTime)
            {
                requests.Add(BuildRequest(
                    patient.Id,
                    pendingCount,
                    localToday,
                    PrimarySlot,
                    isFollowUp: false));
                hasPrimary = true;
                _logger.LogInformation(
                    "Queue primary exercise reminder for patient {PatientId}: local {LocalTime} >= {Preferred}, pending={Pending}.",
                    patient.Id,
                    localTime,
                    patient.PreferredReminderTime,
                    pendingCount);
            }
            else if (!hasPrimary)
            {
                _logger.LogDebug(
                    "Patient {PatientId}: waiting for preferred time (local {LocalTime} < {Preferred}).",
                    patient.Id,
                    localTime,
                    patient.PreferredReminderTime);
            }
            else
            {
                _logger.LogDebug(
                    "Patient {PatientId}: primary reminder already sent for {LocalToday}.",
                    patient.Id,
                    localToday);
            }

            if (patient.ReminderFollowUpEnabled
                && hasPrimary
                && !hasFollowUp
                && localTime >= patient.ReminderFollowUpTime)
            {
                requests.Add(BuildRequest(
                    patient.Id,
                    pendingCount,
                    localToday,
                    FollowUpSlot,
                    isFollowUp: true));
                _logger.LogInformation(
                    "Queue follow-up exercise reminder for patient {PatientId}: local {LocalTime} >= {FollowUp}.",
                    patient.Id,
                    localTime,
                    patient.ReminderFollowUpTime);
            }
        }

        if (requests.Count == 0)
        {
            return;
        }

        await notifications.CreateManyAsync(requests, cancellationToken);
        _logger.LogInformation(
            "Created {Count} exercise reminders at {UtcNow:o}.",
            requests.Count,
            utcNow);
    }

    private static CreateNotificationRequest BuildRequest(
        Guid patientId,
        int pendingCount,
        DateOnly localToday,
        string slot,
        bool isFollowUp)
    {
        var title = isFollowUp ? "Exercise follow-up" : "Exercise reminder";
        var body = isFollowUp
            ? pendingCount == 1
                ? "You still have 1 exercise left today."
                : $"You still have {pendingCount} exercises left today."
            : pendingCount == 1
                ? "You have 1 exercise scheduled for today."
                : $"You have {pendingCount} exercises scheduled for today.";

        return new CreateNotificationRequest(
            patientId,
            NotificationType.ExerciseReminder,
            title,
            body,
            $"{{\"count\":{pendingCount},\"date\":\"{localToday:yyyy-MM-dd}\",\"slot\":\"{slot}\"}}");
    }

    private static bool HasSlotForDate(
        string? data,
        DateTime createdAtUtc,
        DateOnly localToday,
        TimeZoneInfo timeZone,
        string slot,
        TimeOnly scheduledTime)
    {
        var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            timeZone);
        var wasCreatedAfterCurrentSchedule =
            TimeOnly.FromDateTime(createdLocal) >= scheduledTime;

        if (!string.IsNullOrWhiteSpace(data)
            && data.Contains($"\"date\":\"{localToday:yyyy-MM-dd}\"", StringComparison.Ordinal)
            && data.Contains($"\"slot\":\"{slot}\"", StringComparison.Ordinal)
            && wasCreatedAfterCurrentSchedule)
        {
            return true;
        }

        // Legacy primary reminders (no slot) still count as primary for the local day.
        if (slot == PrimarySlot
            && !string.IsNullOrWhiteSpace(data)
            && data.Contains($"\"date\":\"{localToday:yyyy-MM-dd}\"", StringComparison.Ordinal)
            && !data.Contains("\"slot\":", StringComparison.Ordinal)
            && wasCreatedAfterCurrentSchedule)
        {
            return true;
        }

        if (slot == PrimarySlot
            && (string.IsNullOrWhiteSpace(data) || !data.Contains("\"slot\":", StringComparison.Ordinal)))
        {
            return DateOnly.FromDateTime(createdLocal) == localToday
                && wasCreatedAfterCurrentSchedule;
        }

        return false;
    }
}
