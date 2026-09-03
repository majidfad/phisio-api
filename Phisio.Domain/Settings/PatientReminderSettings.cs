using Phisio.Domain.Enums;

namespace Phisio.Domain.Settings;

/// <summary>
/// Patient-owned exercise reminder preferences, separate from identity/care concerns.
/// </summary>
public sealed record PatientReminderSettings(
    bool ExerciseRemindersEnabled,
    TimeOnly PreferredReminderTime,
    string TimeZoneId,
    ReminderRepeatMode RepeatMode,
    int DaysOfWeekMask,
    int IntervalDays,
    DateOnly? AnchorDate,
    bool FollowUpEnabled,
    TimeOnly FollowUpTime)
{
    public const string DefaultTimeZoneId = "Asia/Tehran";

    public static readonly TimeOnly DefaultPreferredTime = new(9, 0);

    public static readonly TimeOnly DefaultFollowUpTime = new(18, 0);

    public static PatientReminderSettings CreateDefault() =>
        new(
            ExerciseRemindersEnabled: true,
            PreferredReminderTime: DefaultPreferredTime,
            TimeZoneId: DefaultTimeZoneId,
            RepeatMode: ReminderRepeatMode.Daily,
            DaysOfWeekMask: ReminderSchedule.AllDaysMask,
            IntervalDays: 1,
            AnchorDate: null,
            FollowUpEnabled: false,
            FollowUpTime: DefaultFollowUpTime);

    public int EffectiveDaysOfWeekMask =>
        DaysOfWeekMask == 0 ? ReminderSchedule.AllDaysMask : DaysOfWeekMask;

    public int EffectiveIntervalDays => Math.Max(1, IntervalDays);

    public bool IsReminderDay(DateOnly localDate) =>
        ReminderSchedule.IsReminderDay(
            localDate,
            RepeatMode,
            EffectiveDaysOfWeekMask,
            EffectiveIntervalDays,
            AnchorDate);
}
