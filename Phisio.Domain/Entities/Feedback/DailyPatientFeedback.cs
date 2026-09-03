using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class DailyPatientFeedback : BaseEntity
{
    public const int MinScore = 1;
    public const int MaxScore = 5;

    public Guid DailyPatientFeedbackId { get; set; }

    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid ClinicId { get; set; }

    public DateOnly FeedbackDate { get; set; }

    public int ImprovementScore { get; set; }

    public int HardnessScore { get; set; }

    public string? Comment { get; set; }

    public CareContext ToCareContext() => CareContext.From(DoctorId, PatientId, ClinicId);

    public static DailyPatientFeedback Submit(
        CareContext context,
        DateOnly feedbackDate,
        int improvementScore,
        int hardnessScore,
        string? comment,
        Guid? dailyPatientFeedbackId = null)
    {
        context.EnsureValid();
        ValidateScore(improvementScore, nameof(improvementScore));
        ValidateScore(hardnessScore, nameof(hardnessScore));

        return new DailyPatientFeedback
        {
            DailyPatientFeedbackId = dailyPatientFeedbackId ?? Guid.NewGuid(),
            DoctorId = context.DoctorId,
            PatientId = context.PatientId,
            ClinicId = context.ClinicId,
            FeedbackDate = feedbackDate,
            ImprovementScore = improvementScore,
            HardnessScore = hardnessScore,
            Comment = comment,
            IsEnabled = true,
        };
    }

    public void UpdateScores(int improvementScore, int hardnessScore, string? comment)
    {
        ValidateScore(improvementScore, nameof(improvementScore));
        ValidateScore(hardnessScore, nameof(hardnessScore));
        ImprovementScore = improvementScore;
        HardnessScore = hardnessScore;
        Comment = comment;
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
