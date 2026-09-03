using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class UserExercise : BaseEntity
{
    public Guid UserExerciseId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid PatientId { get; set; }

    public Guid ClinicId { get; set; }

    public Guid ExerciseId { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public bool IsActive { get; set; }

    public int? Sets { get; set; }

    public string? Reps { get; set; }

    public string? ClinicianNote { get; set; }

    public string? PatientCue { get; set; }

    public Guid? ProgramId { get; set; }

    public Exercise Exercise { get; set; } = null!;

    public ExerciseProgram? Program { get; set; }

    public CareContext ToCareContext() => CareContext.From(DoctorId, PatientId, ClinicId);

    /// <summary>
    /// Creates an ad-hoc assignment (no program) for a clinic-scoped care context.
    /// </summary>
    public static UserExercise CreateAdHoc(
        CareContext context,
        Guid exerciseId,
        DateOnly scheduledDate,
        DateTime assignedAt,
        Guid? userExerciseId = null)
    {
        context.EnsureValid();

        return new UserExercise
        {
            UserExerciseId = userExerciseId ?? Guid.NewGuid(),
            DoctorId = context.DoctorId,
            PatientId = context.PatientId,
            ClinicId = context.ClinicId,
            ExerciseId = exerciseId,
            AssignedAt = assignedAt,
            ScheduledDate = scheduledDate,
            IsActive = true,
            IsEnabled = true,
        };
    }

    /// <summary>
    /// Creates an assignment materialized from a program template.
    /// Enforces that assignment clinic context matches the program.
    /// </summary>
    public static UserExercise CreateFromProgram(
        ExerciseProgram program,
        Guid exerciseId,
        DateOnly scheduledDate,
        DateTime assignedAt,
        Guid? userExerciseId = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        var assignment = CreateAdHoc(
            CareContext.From(program.DoctorId, program.PatientId, program.ClinicId),
            exerciseId,
            scheduledDate,
            assignedAt,
            userExerciseId);

        assignment.LinkToProgram(program);
        return assignment;
    }

    /// <summary>
    /// Associates this assignment with a program, enforcing matching care context.
    /// </summary>
    public void LinkToProgram(ExerciseProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        if (!ToCareContext().Matches(program.DoctorId, program.PatientId, program.ClinicId))
        {
            throw new DomainException(
                "UserExercise care context must match ExerciseProgram (DoctorId, PatientId, ClinicId).");
        }

        ProgramId = program.ProgramId;
    }

    /// <summary>
    /// Applies the latest assigned dosage in place so the same exercise/day
    /// stays a single consolidated schedule row (no duplicates).
    /// </summary>
    public void ApplyLatestDosage(
        DateTime assignedAt,
        int? sets,
        string? reps,
        string? clinicianNote,
        string? patientCue)
    {
        AssignedAt = assignedAt;
        Sets = sets;
        Reps = reps;
        ClinicianNote = clinicianNote;
        PatientCue = patientCue;
    }

    public void Reactivate(DateTime assignedAt, Guid? programId = null)
    {
        IsActive = true;
        IsEnabled = true;
        AssignedAt = assignedAt;
        if (programId.HasValue)
        {
            ProgramId = programId;
        }
    }

    public void Retire()
    {
        IsActive = false;
        IsEnabled = false;
    }
}
