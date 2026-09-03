namespace Phisio.Application.PatientDailyFeedback;

public sealed record SubmitDailyFeedbackResponse(
    Guid DailyPatientFeedbackId,
    Guid PatientId,
    Guid DoctorId,
    Guid ClinicId,
    DateOnly FeedbackDate,
    int ImprovementScore,
    int HardnessScore,
    string? Comment,
    bool WasUpdated);
