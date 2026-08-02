using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class ProgramExercise : BaseEntity
{
    public Guid ProgramExerciseId { get; set; }

    public Guid ProgramId { get; set; }

    public Guid ExerciseId { get; set; }

    public int? Sets { get; set; }

    public string? Reps { get; set; }

    public string? ClinicianNote { get; set; }

    public string? PatientCue { get; set; }

    public ExerciseProgram Program { get; set; } = null!;

    public Exercise Exercise { get; set; } = null!;
}
