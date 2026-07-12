namespace Phisio.Application.Auth;

public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(AccessTokenGenerationRequest request);
}
