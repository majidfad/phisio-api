using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class DailyPatientFeedback : BaseEntity
{
    public Guid DailyPatientFeedbackId { get; set; }

    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public DateOnly FeedbackDate { get; set; }

    public int ImprovementScore { get; set; }

    public int HardnessScore { get; set; }

    public string? Comment { get; set; }
}
