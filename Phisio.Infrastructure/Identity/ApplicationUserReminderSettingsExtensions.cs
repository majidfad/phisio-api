using Phisio.Domain.Settings;
using Phisio.Infrastructure.Services;

namespace Phisio.Infrastructure.Identity;

internal static class ApplicationUserReminderSettingsExtensions
{
    internal static PatientReminderSettings ToReminderSettings(this ApplicationUser user) =>
        new(
            user.ExerciseRemindersEnabled,
            user.PreferredReminderTime,
            string.IsNullOrWhiteSpace(user.TimeZoneId)
                ? PatientReminderSettings.DefaultTimeZoneId
                : user.TimeZoneId,
            user.ReminderRepeatMode,
            user.ReminderDaysOfWeekMask,
            user.ReminderIntervalDays,
            user.ReminderAnchorDate,
            user.ReminderFollowUpEnabled,
            user.ReminderFollowUpTime);

    internal static void ApplyReminderSettings(this ApplicationUser user, PatientReminderSettings settings)
    {
        user.ExerciseRemindersEnabled = settings.ExerciseRemindersEnabled;
        user.PreferredReminderTime = settings.PreferredReminderTime;
        user.TimeZoneId = settings.TimeZoneId;
        user.ReminderRepeatMode = settings.RepeatMode;
        user.ReminderDaysOfWeekMask = settings.EffectiveDaysOfWeekMask;
        user.ReminderIntervalDays = settings.EffectiveIntervalDays;
        user.ReminderAnchorDate = settings.AnchorDate;
        user.ReminderFollowUpEnabled = settings.FollowUpEnabled;
        user.ReminderFollowUpTime = settings.FollowUpTime;
    }

    internal static TimeZoneInfo ResolveTimeZone(this PatientReminderSettings settings) =>
        PatientSettingsService.ResolveTimeZone(settings.TimeZoneId);
}
