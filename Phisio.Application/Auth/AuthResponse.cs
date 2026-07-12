using Phisio.Domain.Enums;



namespace Phisio.Application.Auth;



public sealed record AuthResponse(

    string AccessToken,

    DateTime ExpiresAt,

    Guid UserId,

    string PhoneNumber,

    string? Email,

    string Name,

    UserRole Role);

