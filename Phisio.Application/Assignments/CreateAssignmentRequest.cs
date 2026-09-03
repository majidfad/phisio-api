namespace Phisio.Application.Assignments;

public sealed class CreateAssignmentRequest
{
    public Guid PatientId { get; set; }

    public Guid ExerciseId { get; set; }

    public Guid ClinicId { get; set; }
}
