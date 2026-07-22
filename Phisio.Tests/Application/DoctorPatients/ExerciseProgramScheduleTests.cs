using FluentAssertions;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Application.DoctorPatients;

public class ExerciseProgramScheduleTests
{
    [Fact]
    public void Expand_DaysOfWeek_IncludesMatchingDaysOnly()
    {
        var mask = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Wednesday);
        var dates = ExerciseProgramSchedule.Expand(
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 26),
            ExerciseProgramCadenceType.DaysOfWeek,
            mask,
            null);

        dates.Should().Equal(
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 22));
    }

    [Fact]
    public void Expand_Interval_EveryThreeDays()
    {
        var dates = ExerciseProgramSchedule.Expand(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 10),
            ExerciseProgramCadenceType.Interval,
            0,
            3);

        dates.Should().Equal(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 4),
            new DateOnly(2026, 7, 7),
            new DateOnly(2026, 7, 10));
    }

    [Fact]
    public void ExpandFrom_SkipsPastDates()
    {
        var mask = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Friday);
        var dates = ExerciseProgramSchedule.ExpandFrom(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            new DateOnly(2026, 7, 20),
            ExerciseProgramCadenceType.DaysOfWeek,
            mask,
            null);

        dates.Should().OnlyContain(d => d >= new DateOnly(2026, 7, 20));
        dates.Should().Contain(new DateOnly(2026, 7, 20));
        dates.Should().Contain(new DateOnly(2026, 7, 24));
    }
}
