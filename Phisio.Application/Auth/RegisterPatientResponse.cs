using Phisio.Domain.Enums;

namespace Phisio.Application.Auth;

public sealed record RegisterPatientResponse(
    Guid UserId,
    string PhoneNumber,
    string Name,
    UserRole Role);
