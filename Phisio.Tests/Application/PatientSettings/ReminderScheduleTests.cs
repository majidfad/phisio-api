using FluentAssertions;
using Phisio.Application.PatientSettings;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Application.PatientSettings;

public class ReminderScheduleTests
{
    [Fact]
    public void IsReminderDay_Daily_AlwaysTrue()
    {
        var monday = new DateOnly(2026, 8, 3);
        ReminderSchedule.IsReminderDay(
                monday,
                ReminderRepeatMode.Daily,
                daysOfWeekMask: 0,
                intervalDays: 1,
                anchorDate: null)
            .Should().BeTrue();
    }

    [Fact]
    public void IsReminderDay_DaysOfWeek_RespectsMask()
    {
        var monday = new DateOnly(2026, 8, 3); // Monday
        var tuesday = new DateOnly(2026, 8, 4);
        var maskMonWedFri = (1 << 1) | (1 << 3) | (1 << 5);

        ReminderSchedule.IsReminderDay(
                monday,
                ReminderRepeatMode.DaysOfWeek,
                maskMonWedFri,
                intervalDays: 1,
                anchorDate: null)
            .Should().BeTrue();

        ReminderSchedule.IsReminderDay(
                tuesday,
                ReminderRepeatMode.DaysOfWeek,
                maskMonWedFri,
                intervalDays: 1,
                anchorDate: null)
            .Should().BeFalse();
    }

    [Fact]
    public void IsReminderDay_Interval_UsesAnchor()
    {
        var anchor = new DateOnly(2026, 8, 1);
        ReminderSchedule.IsReminderDay(
                new DateOnly(2026, 8, 1),
                ReminderRepeatMode.Interval,
                daysOfWeekMask: 0,
                intervalDays: 3,
                anchorDate: anchor)
            .Should().BeTrue();

        ReminderSchedule.IsReminderDay(
                new DateOnly(2026, 8, 4),
                ReminderRepeatMode.Interval,
                daysOfWeekMask: 0,
                intervalDays: 3,
                anchorDate: anchor)
            .Should().BeTrue();

        ReminderSchedule.IsReminderDay(
                new DateOnly(2026, 8, 3),
                ReminderRepeatMode.Interval,
                daysOfWeekMask: 0,
                intervalDays: 3,
                anchorDate: anchor)
            .Should().BeFalse();
    }
}
