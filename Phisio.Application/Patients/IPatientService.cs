using Phisio.Application.Common;

namespace Phisio.Application.Patients;

public interface IPatientService
{
    /// <summary>
    /// Legacy patient list. Clinic is intentionally ignored: this DTO has no clinic fields
    /// and may return duplicate patients when the same person is linked in multiple clinics.
    /// Prefer <c>GET api/doctor/patients</c> for clinic-aware listing.
    /// </summary>
    Task<AuthResult<IReadOnlyList<PatientDto>>> GetPatientsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy patient lookup. Ownership is DoctorId + PatientId only (any approved clinic).
    /// </summary>
    Task<AuthResult<PatientDto>> GetPatientByIdAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);
}
