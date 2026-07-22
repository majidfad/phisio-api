using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorPatients;

/// <summary>Expands program cadence rules into concrete schedule dates.</summary>
public static class ExerciseProgramSchedule
{
    public static IReadOnlyList<DateOnly> Expand(
        DateOnly startDate,
        DateOnly endDate,
        ExerciseProgramCadenceType cadenceType,
        int daysOfWeekMask,
        int? intervalDays)
    {
        if (endDate < startDate)
        {
            return [];
        }

        return cadenceType switch
        {
            ExerciseProgramCadenceType.Interval => ExpandInterval(startDate, endDate, intervalDays ?? 1),
            _ => ExpandDaysOfWeek(startDate, endDate, daysOfWeekMask),
        };
    }

    public static IReadOnlyList<DateOnly> ExpandFrom(
        DateOnly startDate,
        DateOnly endDate,
        DateOnly fromInclusive,
        ExerciseProgramCadenceType cadenceType,
        int daysOfWeekMask,
        int? intervalDays)
    {
        var effectiveStart = fromInclusive > startDate ? fromInclusive : startDate;
        return Expand(effectiveStart, endDate, cadenceType, daysOfWeekMask, intervalDays);
    }

    private static IReadOnlyList<DateOnly> ExpandDaysOfWeek(
        DateOnly startDate,
        DateOnly endDate,
        int daysOfWeekMask)
    {
        if (daysOfWeekMask == 0)
        {
            return [];
        }

        var dates = new List<DateOnly>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if ((daysOfWeekMask & (1 << (int)date.DayOfWeek)) != 0)
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static IReadOnlyList<DateOnly> ExpandInterval(
        DateOnly startDate,
        DateOnly endDate,
        int intervalDays)
    {
        var step = Math.Max(1, intervalDays);
        var dates = new List<DateOnly>();
        for (var date = startDate; date <= endDate; date = date.AddDays(step))
        {
            dates.Add(date);
        }

        return dates;
    }
}
