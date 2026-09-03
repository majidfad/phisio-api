using FluentAssertions;
using Phisio.Application.PatientSettings;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Background;
using System.Reflection;

namespace Phisio.Tests.Application.PatientSettings;

/// <summary>
/// Documents the gates ExerciseReminderBackgroundService uses before CreateManyAsync.
/// </summary>
public class ExerciseReminderEligibilityTests
{
    [Fact]
    public void PreferredTime_Gate_Requires_LocalTime_OnOrAfter_Preferred()
    {
        var preferred = new TimeOnly(19, 30);
        (new TimeOnly(19, 29) >= preferred).Should().BeFalse();
        (new TimeOnly(19, 30) >= preferred).Should().BeTrue();
        (new TimeOnly(19, 31) >= preferred).Should().BeTrue();
    }

    [Fact]
    public void ReminderDay_Daily_DoesNotBlock()
    {
        ReminderSchedule.IsReminderDay(
                new DateOnly(2026, 8, 24),
                ReminderRepeatMode.Daily,
                daysOfWeekMask: ReminderSchedule.AllDaysMask,
                intervalDays: 1,
                anchorDate: null)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("19:00", false)]
    [InlineData("19:30", true)]
    [InlineData("20:00", true)]
    public void ShouldQueuePrimary_WhenTimeReached_AndNotAlreadySent(string localTimeText, bool shouldQueue)
    {
        var localTime = TimeOnly.Parse(localTimeText);
        var preferred = new TimeOnly(19, 30);
        const bool hasPrimary = false;
        const int pendingCount = 2;

        var shouldCreate = pendingCount > 0 && !hasPrimary && localTime >= preferred;
        shouldCreate.Should().Be(shouldQueue);
    }

    [Fact]
    public void ShouldNotQueuePrimary_WhenNoPendingExercises()
    {
        var localTime = new TimeOnly(20, 0);
        var preferred = new TimeOnly(19, 30);
        const bool hasPrimary = false;
        const int pendingCount = 0;

        var shouldCreate = pendingCount > 0 && !hasPrimary && localTime >= preferred;
        shouldCreate.Should().BeFalse(
            "ExerciseReminderBackgroundService skips CreateManyAsync when pendingCount == 0");
    }

    [Fact]
    public void ShouldNotQueuePrimary_WhenAlreadySentToday()
    {
        var localTime = new TimeOnly(20, 0);
        var preferred = new TimeOnly(19, 30);
        const bool hasPrimary = true;
        const int pendingCount = 3;

        var shouldCreate = pendingCount > 0 && !hasPrimary && localTime >= preferred;
        shouldCreate.Should().BeFalse();
    }

    [Fact]
    public void Dedup_DoesNotSuppressReminder_WhenScheduleMovedLaterToday()
    {
        var localDate = new DateOnly(2026, 8, 24);
        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Test/Tehran",
            TimeSpan.FromHours(3.5),
            "Test Tehran",
            "Test Tehran");
        var existingCreatedAtUtc = new DateTime(2026, 8, 23, 22, 10, 0, DateTimeKind.Utc);

        HasSlotForDate(
                """{"count":6,"date":"2026-08-24","slot":"primary"}""",
                existingCreatedAtUtc,
                localDate,
                timeZone,
                "primary",
                new TimeOnly(19, 30))
            .Should().BeFalse(
                "the old 01:40 local reminder predates the newly saved 19:30 schedule");
    }

    [Fact]
    public void Dedup_SuppressesReminder_CreatedAfterCurrentScheduledTime()
    {
        var localDate = new DateOnly(2026, 8, 24);
        var timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Test/Tehran",
            TimeSpan.FromHours(3.5),
            "Test Tehran",
            "Test Tehran");
        var existingCreatedAtUtc = new DateTime(2026, 8, 24, 16, 1, 0, DateTimeKind.Utc);

        HasSlotForDate(
                """{"count":6,"date":"2026-08-24","slot":"primary"}""",
                existingCreatedAtUtc,
                localDate,
                timeZone,
                "primary",
                new TimeOnly(19, 30))
            .Should().BeTrue();
    }

    private static bool HasSlotForDate(
        string data,
        DateTime createdAtUtc,
        DateOnly localToday,
        TimeZoneInfo timeZone,
        string slot,
        TimeOnly scheduledTime)
    {
        var method = typeof(ExerciseReminderBackgroundService).GetMethod(
            "HasSlotForDate",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        return (bool)method!.Invoke(
            null,
            [data, createdAtUtc, localToday, timeZone, slot, scheduledTime])!;
    }
}
