using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

/// <summary>
/// A clinic managed by a single <see cref="ClinicManagerId"/> user who must also appear as a doctor
/// of the clinic via <see cref="ClinicDoctor"/> (same <c>ApplicationUser</c>, no separate entity).
/// </summary>
public class Clinic : BaseEntity
{
    public Guid ClinicId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public Guid ClinicManagerId { get; set; }

    public ICollection<ClinicPhoneNumber> PhoneNumbers { get; set; } = new List<ClinicPhoneNumber>();

    public ICollection<ClinicDoctor> ClinicDoctors { get; set; } = new List<ClinicDoctor>();

    public bool HasManagerDoctorMembership() =>
        ClinicDoctors.Any(link => link.DoctorId == ClinicManagerId);

    /// <summary>
    /// Ensures the clinic manager is also represented as a doctor of this clinic.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    public void EnsureManagerDoctorMembership()
    {
        if (HasManagerDoctorMembership())
        {
            return;
        }

        ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = ClinicId,
            DoctorId = ClinicManagerId,
            Clinic = this,
        });
    }
}
