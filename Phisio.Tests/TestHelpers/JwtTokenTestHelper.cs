using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Phisio.Tests.TestHelpers;

internal static class JwtTokenTestHelper
{
    public static JwtSecurityToken ReadToken(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

    public static IReadOnlyList<Claim> GetClaims(string accessToken) =>
        ReadToken(accessToken).Claims.ToList();

    public static IReadOnlyList<string> GetRoleClaims(string accessToken) =>
        GetClaims(accessToken)
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();
}
