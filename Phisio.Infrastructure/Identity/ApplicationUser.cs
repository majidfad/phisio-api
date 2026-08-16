using Microsoft.AspNetCore.Identity;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>, IAuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// When false, the daily exercise reminder job skips this patient.
    /// </summary>
    public bool ExerciseRemindersEnabled { get; set; } = true;

    /// <summary>
    /// Local time of day (in <see cref="TimeZoneId"/>) when the primary reminder may be sent.
    /// </summary>
    public TimeOnly PreferredReminderTime { get; set; } = new(9, 0);

    /// <summary>
    /// IANA time zone id used for reminder scheduling (e.g. Asia/Tehran).
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Tehran";

    /// <summary>How often reminders repeat.</summary>
    public ReminderRepeatMode ReminderRepeatMode { get; set; } = ReminderRepeatMode.Daily;

    /// <summary>
    /// Bitmask of weekdays (Sunday = bit 0) used when <see cref="ReminderRepeatMode"/> is DaysOfWeek.
    /// </summary>
    public int ReminderDaysOfWeekMask { get; set; } = 0b1111111;

    /// <summary>
    /// Interval in days used when <see cref="ReminderRepeatMode"/> is Interval.
    /// </summary>
    public int ReminderIntervalDays { get; set; } = 1;

    /// <summary>
    /// Anchor local date for interval cadence (usually the day settings were saved).
    /// </summary>
    public DateOnly? ReminderAnchorDate { get; set; }

    /// <summary>Send a second reminder later the same day if exercises are still incomplete.</summary>
    public bool ReminderFollowUpEnabled { get; set; }

    /// <summary>Local time for the optional follow-up reminder.</summary>
    public TimeOnly ReminderFollowUpTime { get; set; } = new(18, 0);

    public ICollection<UserExercise> UserExercises { get; set; } = new List<UserExercise>();

    public DoctorProfile? DoctorProfile { get; set; }

    public ICollection<Clinic> ManagedClinics { get; set; } = new List<Clinic>();
}
