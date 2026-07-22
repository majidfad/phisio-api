using Phisio.Domain.Common;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

public class UserExercise : BaseEntity
{
    public Guid UserExerciseId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid PatientId { get; set; }

    public Guid ExerciseId { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public bool IsActive { get; set; }

    public int? Sets { get; set; }

    public string? Reps { get; set; }

    public int? HoldSeconds { get; set; }

    public int? RestSeconds { get; set; }

    public ExerciseSide Side { get; set; } = ExerciseSide.NotApplicable;

    public string? ClinicianNote { get; set; }

    public string? PatientCue { get; set; }

    public Guid? ProgramId { get; set; }

    public Exercise Exercise { get; set; } = null!;

    public ExerciseProgram? Program { get; set; }
}
