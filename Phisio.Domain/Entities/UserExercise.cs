using Phisio.Domain.Common;

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

    public string? ClinicianNote { get; set; }

    public string? PatientCue { get; set; }

    public Guid? ProgramId { get; set; }

    public Exercise Exercise { get; set; } = null!;

    public ExerciseProgram? Program { get; set; }

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
