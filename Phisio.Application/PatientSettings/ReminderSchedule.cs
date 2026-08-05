using Phisio.Domain.Enums;

namespace Phisio.Application.PatientSettings;

/// <summary>Evaluates whether a reminder should fire for a local calendar day.</summary>
public static class ReminderSchedule
{
    public const int AllDaysMask = 0b1111111;

    public static bool IsReminderDay(
        DateOnly localDate,
        ReminderRepeatMode repeatMode,
        int daysOfWeekMask,
        int intervalDays,
        DateOnly? anchorDate)
    {
        return repeatMode switch
        {
            ReminderRepeatMode.DaysOfWeek =>
                daysOfWeekMask != 0 && (daysOfWeekMask & (1 << (int)localDate.DayOfWeek)) != 0,

            ReminderRepeatMode.Interval =>
                IsIntervalDay(localDate, Math.Max(1, intervalDays), anchorDate ?? localDate),

            _ => true, // Daily
        };
    }

    private static bool IsIntervalDay(DateOnly localDate, int intervalDays, DateOnly anchorDate)
    {
        if (localDate < anchorDate)
        {
            return false;
        }

        var daysSinceAnchor = localDate.DayNumber - anchorDate.DayNumber;
        return daysSinceAnchor % intervalDays == 0;
    }
}
