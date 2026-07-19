using Phisio.Application.Common;

namespace Phisio.Application.Auth;

public interface IAuthService
{
    Task<AuthResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<RegisterPatientResponse>> RegisterPatientAsync(
        RegisterPatientRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<MeResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ChangePasswordResponse>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
