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

    public DoctorPatientStatus Status { get; set; } = DoctorPatientStatus.Pending;
}
