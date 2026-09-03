namespace Phisio.Application.PatientVisits;

public sealed record VisitFeedbackDto(
    int SatisfactionScore,
    int DoctorCommunicationScore,
    string? Comment,
    DateTime SubmittedAt);

public sealed record SubmitVisitFeedbackRequest(
    int SatisfactionScore,
    int DoctorCommunicationScore,
    string? Comment);
