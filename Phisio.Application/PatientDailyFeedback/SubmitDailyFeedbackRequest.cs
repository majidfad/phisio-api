namespace Phisio.Application.PatientDailyFeedback;

public sealed class SubmitDailyFeedbackRequest
{
    public Guid? DoctorId { get; set; }

    public int ImprovementScore { get; set; }

    public int HardnessScore { get; set; }

    public string? Comment { get; set; }
}
