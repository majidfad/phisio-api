using FluentAssertions;
using Phisio.Domain.Enums;
using Phisio.Domain.Settings;

namespace Phisio.Tests.DomainModel.Settings;

public class PatientReminderSettingsTests
{
    [Fact]
    public void CreateDefault_EnablesDailyReminders()
    {
        var settings = PatientReminderSettings.CreateDefault();

        settings.ExerciseRemindersEnabled.Should().BeTrue();
        settings.RepeatMode.Should().Be(ReminderRepeatMode.Daily);
        settings.IsReminderDay(new DateOnly(2026, 9, 2)).Should().BeTrue();
    }

    [Fact]
    public void EffectiveDaysOfWeekMask_WhenZero_UsesAllDays()
    {
        var settings = new PatientReminderSettings(
            true,
            PatientReminderSettings.DefaultPreferredTime,
            PatientReminderSettings.DefaultTimeZoneId,
            ReminderRepeatMode.DaysOfWeek,
            DaysOfWeekMask: 0,
            IntervalDays: 1,
            AnchorDate: null,
            FollowUpEnabled: false,
            PatientReminderSettings.DefaultFollowUpTime);

        settings.EffectiveDaysOfWeekMask.Should().Be(ReminderSchedule.AllDaysMask);
    }
}
