namespace Phisio.Application.Auth;



public sealed record MeResponse(

    Guid UserId,

    string PhoneNumber,

    string? Email,

    IReadOnlyList<string> Roles);

