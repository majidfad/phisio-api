using Phisio.Application.Common;

namespace Phisio.Application.Patients;

public interface IPatientService
{
    Task<AuthResult<IReadOnlyList<PatientDto>>> GetPatientsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientDto>> GetPatientByIdAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);
}
