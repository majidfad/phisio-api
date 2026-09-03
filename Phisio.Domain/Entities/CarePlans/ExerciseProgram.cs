using Phisio.Domain.Common;
using Phisio.Domain.CarePlans;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

public class ExerciseProgram : BaseEntity
{
    public Guid ProgramId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid PatientId { get; set; }

    public Guid ClinicId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public ExerciseProgramCadenceType CadenceType { get; set; } = ExerciseProgramCadenceType.DaysOfWeek;

    /// <summary>Bitmask of <see cref="DayOfWeek"/> values (bit 0 = Sunday).</summary>
    public int DaysOfWeekMask { get; set; }

    /// <summary>Used when <see cref="CadenceType"/> is Interval (every N days).</summary>
    public int? IntervalDays { get; set; }

    public ICollection<ProgramExercise> Exercises { get; set; } = [];

    public ICollection<UserExercise> UserExercises { get; set; } = [];

    public CareContext ToCareContext() => CareContext.From(DoctorId, PatientId, ClinicId);

    public static ExerciseProgram Create(
        CareContext context,
        DateOnly startDate,
        DateOnly endDate,
        ExerciseProgramCadenceType cadenceType,
        int daysOfWeekMask,
        int? intervalDays,
        Guid? programId = null)
    {
        context.EnsureValid();

        return new ExerciseProgram
        {
            ProgramId = programId ?? Guid.NewGuid(),
            DoctorId = context.DoctorId,
            PatientId = context.PatientId,
            ClinicId = context.ClinicId,
            StartDate = startDate,
            EndDate = endDate,
            CadenceType = cadenceType,
            DaysOfWeekMask = daysOfWeekMask,
            IntervalDays = intervalDays,
            IsEnabled = true,
        };
    }

    public void UpdateSchedule(
        DateOnly startDate,
        DateOnly endDate,
        ExerciseProgramCadenceType cadenceType,
        int daysOfWeekMask,
        int? intervalDays)
    {
        StartDate = startDate;
        EndDate = endDate;
        CadenceType = cadenceType;
        DaysOfWeekMask = daysOfWeekMask;
        IntervalDays = intervalDays;
    }

    public void DisableActiveExercises()
    {
        foreach (var programExercise in Exercises.Where(exercise => exercise.IsEnabled))
        {
            programExercise.IsEnabled = false;
        }
    }

    public IReadOnlyList<ProgramExercise> ReplaceExercises(IEnumerable<ProgramExerciseDosage> items)
    {
        DisableActiveExercises();

        var created = new List<ProgramExercise>();
        foreach (var item in items)
        {
            created.Add(ProgramExercise.Create(
                ProgramId,
                item.ExerciseId,
                item.Sets,
                item.Reps,
                item.ClinicianNote,
                item.PatientCue));
        }

        return created;
    }

    public void SoftDeleteWithExercises()
    {
        DisableActiveExercises();
        IsEnabled = false;
    }
}
