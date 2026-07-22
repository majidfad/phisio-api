using Phisio.Domain.Common;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

public class ExerciseProgram : BaseEntity
{
    public Guid ProgramId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid PatientId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ExerciseProgramCadenceType CadenceType { get; set; } = ExerciseProgramCadenceType.DaysOfWeek;

    /// <summary>Bitmask of <see cref="DayOfWeek"/> values (bit 0 = Sunday).</summary>
    public int DaysOfWeekMask { get; set; }

    /// <summary>Used when <see cref="CadenceType"/> is Interval (every N days).</summary>
    public int? IntervalDays { get; set; }

    public ICollection<ProgramExercise> Exercises { get; set; } = [];

    public ICollection<UserExercise> UserExercises { get; set; } = [];
}
