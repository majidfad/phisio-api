using Phisio.Domain.Common;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

public class PatientVisit : BaseEntity
{
    public Guid PatientVisitId { get; set; }

    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid ClinicId { get; set; }

    public DateTime VisitAt { get; set; }

    /// <summary>
    /// Type of visit: Initial, FollowUp, Emergency, Discharge.
    /// </summary>
    public VisitType? VisitType { get; set; }

    /// <summary>
    /// Doctor-assessed patient condition at the time of visit.
    /// </summary>
    public PatientCondition? PatientCondition { get; set; }

    /// <summary>
    /// Doctor-added visit notes (clinical / administrative).
    /// </summary>
    public string? DoctorNotes { get; set; }

    public Clinic Clinic { get; set; } = null!;
}

