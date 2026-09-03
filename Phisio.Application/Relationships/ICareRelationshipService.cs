using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;

namespace Phisio.Application.Relationships;

/// <summary>
/// Clinic-scoped doctor–patient relationship management and care-access gates.
/// </summary>
public interface ICareRelationshipService
{
    Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorPatientRequestDto>>> GetPendingRequestsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorClinicOptionDto>>> GetMyClinicsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientLookupDto>> LookupPatientByPhoneAsync(
        Guid doctorId,
        string? phoneNumber,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientDto>> AddPatientAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> EnsureCareAccessAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveRelationshipAsync(
        Guid doctorId,
        Guid patientId,
        Guid? clinicId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the patient has no other approved/pending care link
    /// (excluding the given doctor+clinic pair being opened or reopened).
    /// </summary>
    Task<AuthResult<bool>> EnsurePatientCanOpenCareLinkAsync(
        Guid patientId,
        Guid doctorId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}
