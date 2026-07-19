using Phisio.Domain.Enums;

namespace Phisio.Application.Auth;

public sealed record RegisterResponse(
    Guid UserId,
    string PhoneNumber,
    string Name,
    UserRole Role,
    string Message);
