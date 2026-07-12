using Phisio.Application.Common;
using Phisio.Application.Patients;

namespace Phisio.Application.Admin.Patients;

public interface IAdminPatientService
{
    Task<AuthResult<IReadOnlyList<PatientDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientDto>> GetByIdAsync(Guid patientId, CancellationToken cancellationToken = default);

    Task<AuthResult<PatientDto>> CreateAsync(
        CreateAdminPatientDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientDto>> UpdateAsync(
        Guid patientId,
        UpdateAdminPatientDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(Guid patientId, CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> ActivateAsync(Guid patientId, CancellationToken cancellationToken = default);
}
