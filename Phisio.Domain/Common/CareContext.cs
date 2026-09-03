namespace Phisio.Domain.Common;

/// <summary>
/// Identifies a clinic-scoped care relationship between a doctor and a patient.
/// Shared across assignments, programs, and feedback.
/// </summary>
public readonly record struct CareContext(Guid DoctorId, Guid PatientId, Guid ClinicId)
{
    public static CareContext From(Guid doctorId, Guid patientId, Guid clinicId) =>
        new(doctorId, patientId, clinicId);

    public void EnsureValid()
    {
        if (DoctorId == Guid.Empty)
        {
            throw new DomainException("DoctorId is required.");
        }

        if (PatientId == Guid.Empty)
        {
            throw new DomainException("PatientId is required.");
        }

        if (ClinicId == Guid.Empty)
        {
            throw new DomainException("ClinicId is required.");
        }
    }

    public bool Matches(Guid doctorId, Guid patientId, Guid clinicId) =>
        DoctorId == doctorId && PatientId == patientId && ClinicId == clinicId;
}
