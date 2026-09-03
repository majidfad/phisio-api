using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Phisio.Application.Notifications;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
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

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

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
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

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
            .ToListAsync(cancellationToken);

        if (patients.Count == 0)
        {
            return;
        }

        var patientSettings = patients
            .Select(user => new
            {
                user.Id,
                Settings = user.ToReminderSettings(),
            })
            .ToList();

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

        foreach (var patient in patientSettings)
        {
            var settings = patient.Settings;
            var timeZone = settings.ResolveTimeZone();
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
            var localToday = DateOnly.FromDateTime(localNow);
            var localTime = TimeOnly.FromDateTime(localNow);

            if (!settings.IsReminderDay(localToday))
            {
                continue;
            }

            var pendingCount = assignments.Count(a =>
                a.PatientId == patient.Id
                && a.ScheduledDate == localToday
                && !completedSet.Contains((a.UserExerciseId, localToday)));

            if (pendingCount == 0)
            {
                continue;
            }

            var patientReminders = recentReminders
                .Where(n => n.UserId == patient.Id)
                .ToList();

            var hasPrimary = patientReminders.Any(n =>
                HasSlotForDate(n.Data, n.CreatedAt, localToday, timeZone, PrimarySlot));
            var hasFollowUp = patientReminders.Any(n =>
                HasSlotForDate(n.Data, n.CreatedAt, localToday, timeZone, FollowUpSlot));

            if (!hasPrimary && localTime >= settings.PreferredReminderTime)
            {
                requests.Add(BuildRequest(
                    patient.Id,
                    pendingCount,
                    localToday,
                    PrimarySlot,
                    isFollowUp: false));
                hasPrimary = true;
            }

            if (settings.FollowUpEnabled
                && hasPrimary
                && !hasFollowUp
                && localTime >= settings.FollowUpTime)
            {
                requests.Add(BuildRequest(
                    patient.Id,
                    pendingCount,
                    localToday,
                    FollowUpSlot,
                    isFollowUp: true));
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
        string slot)
    {
        if (!string.IsNullOrWhiteSpace(data)
            && data.Contains($"\"date\":\"{localToday:yyyy-MM-dd}\"", StringComparison.Ordinal)
            && data.Contains($"\"slot\":\"{slot}\"", StringComparison.Ordinal))
        {
            return true;
        }

        // Legacy primary reminders (no slot) still count as primary for the local day.
        if (slot == PrimarySlot
            && !string.IsNullOrWhiteSpace(data)
            && data.Contains($"\"date\":\"{localToday:yyyy-MM-dd}\"", StringComparison.Ordinal)
            && !data.Contains("\"slot\":", StringComparison.Ordinal))
        {
            return true;
        }

        if (slot == PrimarySlot
            && (string.IsNullOrWhiteSpace(data) || !data.Contains("\"slot\":", StringComparison.Ordinal)))
        {
            var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
                timeZone);
            return DateOnly.FromDateTime(createdLocal) == localToday;
        }

        return false;
    }
}
