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

    public Exercise Exercise { get; set; } = null!;
}
