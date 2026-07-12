namespace Phisio.Application.PatientDailyFeedback;

public sealed class SubmitDailyFeedbackRequest
{
    public int ImprovementScore { get; set; }

    public string? Comment { get; set; }
}
