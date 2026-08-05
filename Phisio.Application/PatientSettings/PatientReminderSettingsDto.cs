using Phisio.Domain.Enums;

namespace Phisio.Application.PatientSettings;

public sealed record PatientReminderSettingsDto(
    bool ExerciseRemindersEnabled,
    string PreferredReminderTime,
    string TimeZoneId,
    ReminderRepeatMode RepeatMode,
    int DaysOfWeekMask,
    int IntervalDays,
    string? AnchorDate,
    bool FollowUpEnabled,
    string FollowUpReminderTime);

public sealed record UpdatePatientReminderSettingsRequest(
    bool ExerciseRemindersEnabled,
    string PreferredReminderTime,
    string? TimeZoneId,
    ReminderRepeatMode RepeatMode,
    int DaysOfWeekMask,
    int IntervalDays,
    bool FollowUpEnabled,
    string? FollowUpReminderTime = null);
