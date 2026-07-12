namespace Phisio.Application.Auth;

public sealed record AccessTokenResult(string AccessToken, DateTime ExpiresAt);
