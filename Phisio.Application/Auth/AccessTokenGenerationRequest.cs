namespace Phisio.Application.Auth;

public sealed record AccessTokenGenerationRequest(
    Guid UserId,
    string UserName,
    string Name,
    IEnumerable<string> Roles);
