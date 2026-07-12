using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

/// <summary>
/// Links a doctor to a patient. Soft-deleted relationships set <see cref="BaseEntity.IsEnabled"/> to false.
/// </summary>
public class DoctorPatient : BaseEntity
{
    public Guid DoctorId { get; set; }

    public Guid PatientId { get; set; }
}
