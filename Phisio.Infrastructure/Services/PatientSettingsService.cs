using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.PatientSettings;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Services;

public class PatientSettingsService : IPatientSettingsService
{
    public const string DefaultTimeZoneId = "Asia/Tehran";
    public static readonly TimeOnly DefaultReminderTime = new(9, 0);
    public static readonly TimeOnly DefaultFollowUpTime = new(18, 0);

    private readonly UserManager<ApplicationUser> _userManager;

    public PatientSettingsService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<AuthResult<PatientReminderSettingsDto>> GetReminderSettingsAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await FindPatientAsync(patientId, cancellationToken);
        if (patient is null)
        {
            return AuthResult<PatientReminderSettingsDto>.Failure(["Patient not found."]);
        }

        return AuthResult<PatientReminderSettingsDto>.Success(Map(patient));
    }

    public async Task<AuthResult<PatientReminderSettingsDto>> UpdateReminderSettingsAsync(
        Guid patientId,
        UpdatePatientReminderSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var patient = await FindPatientAsync(patientId, cancellationToken);
        if (patient is null)
        {
            return AuthResult<PatientReminderSettingsDto>.Failure(["Patient not found."]);
        }

        if (!TimeOnly.TryParse(request.PreferredReminderTime, out var preferredTime))
        {
            return AuthResult<PatientReminderSettingsDto>.Failure(
                ["Preferred reminder time must be a valid time (HH:mm or HH:mm:ss)."]);
        }

        var followUpRaw = string.IsNullOrWhiteSpace(request.FollowUpReminderTime)
            ? DefaultFollowUpTime.ToString("HH:mm")
            : request.FollowUpReminderTime;

        if (!TimeOnly.TryParse(followUpRaw, out var followUpTime))
        {
            return AuthResult<PatientReminderSettingsDto>.Failure(
                ["Follow-up reminder time must be a valid time (HH:mm or HH:mm:ss)."]);
        }

        var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
            ? (string.IsNullOrWhiteSpace(patient.TimeZoneId) ? DefaultTimeZoneId : patient.TimeZoneId)
            : request.TimeZoneId.Trim();

        if (!TryResolveTimeZone(timeZoneId, out var timeZone))
        {
            return AuthResult<PatientReminderSettingsDto>.Failure(["Time zone is not valid."]);
        }

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var intervalDays = Math.Clamp(request.IntervalDays <= 0 ? 1 : request.IntervalDays, 1, 30);
        var daysMask = request.RepeatMode == ReminderRepeatMode.Daily
            ? ReminderSchedule.AllDaysMask
            : Math.Clamp(request.DaysOfWeekMask, 0, ReminderSchedule.AllDaysMask);

        patient.ExerciseRemindersEnabled = request.ExerciseRemindersEnabled;
        patient.PreferredReminderTime = preferredTime;
        patient.TimeZoneId = timeZoneId;
        patient.ReminderRepeatMode = request.RepeatMode;
        patient.ReminderDaysOfWeekMask = daysMask;
        patient.ReminderIntervalDays = intervalDays;
        patient.ReminderFollowUpEnabled = request.FollowUpEnabled;
        patient.ReminderFollowUpTime = followUpTime;

        if (request.RepeatMode == ReminderRepeatMode.Interval)
        {
            // Reset the cadence anchor whenever interval settings are saved.
            patient.ReminderAnchorDate = localToday;
        }
        else if (patient.ReminderAnchorDate is null)
        {
            patient.ReminderAnchorDate = localToday;
        }

        var updateResult = await _userManager.UpdateAsync(patient);
        if (!updateResult.Succeeded)
        {
            return AuthResult<PatientReminderSettingsDto>.Failure(
                updateResult.Errors.Select(e => e.Description));
        }

        return AuthResult<PatientReminderSettingsDto>.Success(Map(patient));
    }

    private async Task<ApplicationUser?> FindPatientAsync(
        Guid patientId,
        CancellationToken cancellationToken) =>
        await _userManager.Users
            .FirstOrDefaultAsync(
                u => u.Id == patientId && u.Role == UserRole.Patient && u.IsEnabled,
                cancellationToken);

    private static PatientReminderSettingsDto Map(ApplicationUser patient) =>
        new(
            patient.ExerciseRemindersEnabled,
            patient.PreferredReminderTime.ToString("HH:mm"),
            string.IsNullOrWhiteSpace(patient.TimeZoneId) ? DefaultTimeZoneId : patient.TimeZoneId,
            patient.ReminderRepeatMode,
            patient.ReminderDaysOfWeekMask == 0
                ? ReminderSchedule.AllDaysMask
                : patient.ReminderDaysOfWeekMask,
            Math.Max(1, patient.ReminderIntervalDays),
            patient.ReminderAnchorDate?.ToString("yyyy-MM-dd"),
            patient.ReminderFollowUpEnabled,
            patient.ReminderFollowUpTime.ToString("HH:mm"));

    public static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            if (timeZoneId.Equals(DefaultTimeZoneId, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
                    return true;
                }
                catch (TimeZoneNotFoundException)
                {
                    // fall through
                }
            }
        }
        catch (InvalidTimeZoneException)
        {
            // fall through
        }

        timeZone = TimeZoneInfo.Utc;
        return false;
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        if (TryResolveTimeZone(id, out var timeZone))
        {
            return timeZone;
        }

        return TimeZoneInfo.Utc;
    }
}
