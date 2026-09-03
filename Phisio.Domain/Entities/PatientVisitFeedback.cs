using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

/// <summary>
/// Patient-submitted feedback for a single clinic visit. One feedback per visit.
/// </summary>
public class PatientVisitFeedback : BaseEntity
{
    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int MaxCommentLength = 1000;

    public Guid PatientVisitFeedbackId { get; set; }

    public Guid PatientVisitId { get; set; }

    /// <summary>
    /// Overall satisfaction with the visit (1–5).
    /// </summary>
    public int SatisfactionScore { get; set; }

    /// <summary>
    /// How clear / attentive the doctor felt (1–5).
    /// </summary>
    public int DoctorCommunicationScore { get; set; }

    public string? Comment { get; set; }

    public PatientVisit Visit { get; set; } = null!;

    public static PatientVisitFeedback Create(
        Guid patientVisitId,
        int satisfactionScore,
        int doctorCommunicationScore,
        string? comment,
        Guid? id = null)
    {
        ValidateScore(satisfactionScore, nameof(satisfactionScore));
        ValidateScore(doctorCommunicationScore, nameof(doctorCommunicationScore));

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmed is not null && trimmed.Length > MaxCommentLength)
        {
            throw new DomainException(
                $"Comment must be at most {MaxCommentLength} characters.");
        }

        return new PatientVisitFeedback
        {
            PatientVisitFeedbackId = id ?? Guid.NewGuid(),
            PatientVisitId = patientVisitId,
            SatisfactionScore = satisfactionScore,
            DoctorCommunicationScore = doctorCommunicationScore,
            Comment = trimmed,
            IsEnabled = true,
        };
    }

    private static void ValidateScore(int score, string paramName)
    {
        if (score is < MinScore or > MaxScore)
        {
            throw new DomainException(
                $"{paramName} must be between {MinScore} and {MaxScore}.");
        }
    }
}
