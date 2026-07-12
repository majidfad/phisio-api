using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class ExerciseCompletion : BaseEntity
{
    public Guid ExerciseCompletionId { get; set; }

    public Guid UserExerciseId { get; set; }

    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid ExerciseId { get; set; }

    public DateOnly CompletionDate { get; set; }
}
