using Phisio.Domain.Common;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

/// <summary>
/// Links a doctor to a patient. Soft-deleted relationships set <see cref="BaseEntity.IsEnabled"/> to false.
/// Active care relationships use <see cref="DoctorPatientStatus.Approved"/>.
/// </summary>
public class DoctorPatient : BaseEntity
{
    public Guid DoctorId { get; set; }

    public Guid PatientId { get; set; }

    public Guid ClinicId { get; set; }

    public Clinic Clinic { get; set; } = null!;

    public DoctorPatientStatus Status { get; set; } = DoctorPatientStatus.Pending;

    public CareContext ToCareContext() => CareContext.From(DoctorId, PatientId, ClinicId);

    public void Approve(DateTime approvedAt)
    {
        Status = DoctorPatientStatus.Approved;
        CreatedAt = approvedAt;
    }

    public void Reject() => Status = DoctorPatientStatus.Rejected;

    public void SoftRemove() => IsEnabled = false;

    public void ReestablishAsApproved(DateTime linkedAt)
    {
        IsEnabled = true;
        Status = DoctorPatientStatus.Approved;
        CreatedAt = linkedAt;
    }

    public static DoctorPatient CreateApproved(CareContext context, DateTime linkedAt)
    {
        context.EnsureValid();
        return new DoctorPatient
        {
            DoctorId = context.DoctorId,
            PatientId = context.PatientId,
            ClinicId = context.ClinicId,
            Status = DoctorPatientStatus.Approved,
            IsEnabled = true,
            CreatedAt = linkedAt,
        };
    }
}
