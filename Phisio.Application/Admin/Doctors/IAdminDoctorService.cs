using Phisio.Application.Common;
using Phisio.Application.Doctors;

namespace Phisio.Application.Admin.Doctors;

public interface IAdminDoctorService
{
    Task<AuthResult<IReadOnlyList<DoctorDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorDto>> GetByIdAsync(Guid doctorId, CancellationToken cancellationToken = default);

    Task<AuthResult<CreateAdminDoctorResponse>> CreateAsync(
        CreateAdminDoctorDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorDto>> UpdateAsync(
        Guid doctorId,
        UpdateAdminDoctorDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(Guid doctorId, CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> ActivateAsync(Guid doctorId, CancellationToken cancellationToken = default);

    Task<AuthResult<AdminSetPasswordResponse>> SetPasswordAsync(
        Guid doctorId,
        AdminSetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
