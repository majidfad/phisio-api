using Phisio.Application.Auth;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Authentication;

namespace Phisio.Tests.TestDataBuilder;

internal static class JwtTestDataBuilder
{
    public static JwtSettings ValidSettings(int expirationMinutes = 60) =>
        new()
        {
            Issuer = "Phisio.Test",
            Audience = "Phisio.Test.Audience",
            SecretKey = "test-secret-key-must-be-at-least-32-characters-long",
            AccessTokenExpirationMinutes = expirationMinutes
        };

    public static AccessTokenGenerationRequest CreateRequest(
        Guid? userId = null,
        string userName = "+15551234567",
        string name = "Dr. Jane Smith",
        IEnumerable<string>? roles = null) =>
        new(
            userId ?? Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            userName,
            name,
            roles ?? [nameof(UserRole.Doctor)]);
}
