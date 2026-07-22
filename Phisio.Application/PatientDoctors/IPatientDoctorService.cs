using Phisio.Application.Common;

namespace Phisio.Application.PatientDoctors;

public interface IPatientDoctorService
{
    Task<AuthResult<IReadOnlyList<PatientDoctorDirectoryItemDto>>> SearchDoctorsAsync(
        Guid patientId,
        string? search,
        string? specialty,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientDoctorProfileDto>> GetDoctorProfileAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<PatientLinkedDoctorDto>>> GetMyDoctorsAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientLinkedDoctorDto>> RequestLinkAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> CancelRequestAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> UnlinkAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default);
}
