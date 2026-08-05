namespace Phisio.Domain.Enums;

/// <summary>
/// How often a patient wants exercise reminders.
/// </summary>
public enum ReminderRepeatMode
{
    /// <summary>Every calendar day (when exercises remain incomplete).</summary>
    Daily = 1,

    /// <summary>Only on selected weekdays (bitmask).</summary>
    DaysOfWeek = 2,

    /// <summary>Every N days from an anchor date.</summary>
    Interval = 3,
}
