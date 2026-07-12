using Phisio.Domain.Entities;

namespace Phisio.Tests.TestDataBuilder;

internal static class DailyPatientFeedbackBuilder
{
    public static DailyPatientFeedback Create(
        Guid patientId,
        Guid doctorId,
        int improvementScore = 4,
        string? comment = null,
        DateOnly? feedbackDate = null,
        bool isEnabled = true,
        Guid? id = null) =>
        new()
        {
            DailyPatientFeedbackId = id ?? Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            FeedbackDate = feedbackDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ImprovementScore = improvementScore,
            Comment = comment,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
        };
}
