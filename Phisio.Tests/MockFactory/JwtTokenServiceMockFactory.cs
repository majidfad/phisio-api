using Moq;
using Phisio.Application.Auth;

namespace Phisio.Tests.MockFactory;

internal static class JwtTokenServiceMockFactory
{
    public static Mock<IJwtTokenService> Create(
        string accessToken = "test-access-token",
        DateTime? expiresAt = null)
    {
        var mock = new Mock<IJwtTokenService>();
        var expiry = expiresAt ?? DateTime.UtcNow.AddHours(1);

        mock.Setup(service => service.GenerateAccessToken(It.IsAny<AccessTokenGenerationRequest>()))
            .Returns(new AccessTokenResult(accessToken, expiry));

        return mock;
    }
}
