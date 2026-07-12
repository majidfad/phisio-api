namespace Phisio.Application.PatientDailyFeedback;

public sealed record SubmitDailyFeedbackResponse(
    Guid DailyPatientFeedbackId,
    Guid PatientId,
    Guid DoctorId,
    DateOnly FeedbackDate,
    int ImprovementScore,
    string? Comment,
    bool WasUpdated);
