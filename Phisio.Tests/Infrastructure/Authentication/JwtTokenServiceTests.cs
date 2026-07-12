using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Phisio.Application.Auth;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Authentication;
using Phisio.Tests.TestDataBuilder;
using Phisio.Tests.TestHelpers;

namespace Phisio.Tests.Infrastructure.Authentication;

public class JwtTokenServiceTests
{
    private const int ExpirationMinutes = 45;

    private static JwtTokenService CreateSut(JwtSettings? settings = null)
    {
        var jwtSettings = settings ?? JwtTestDataBuilder.ValidSettings(ExpirationMinutes);
        return new JwtTokenService(Options.Create(jwtSettings));
    }

    [Fact]
    public void GenerateAccessToken_ReturnsGeneratedToken()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest();
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.AccessToken.Split('.').Should().HaveCount(3, "a JWT must contain header, payload, and signature");
    }

    [Fact]
    public void GenerateAccessToken_SetsExpirationCorrectly()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest();
        var sut = CreateSut();
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        var expectedExpiration = beforeGeneration.AddMinutes(ExpirationMinutes);
        result.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(2));

        var token = JwtTokenTestHelper.ReadToken(result.AccessToken);
        token.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GenerateAccessToken_IncludesUserIdClaim()
    {
        // Arrange
        var userId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var request = JwtTestDataBuilder.CreateRequest(userId: userId);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        var claims = JwtTokenTestHelper.GetClaims(result.AccessToken);

        claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.NameIdentifier && claim.Value == userId.ToString());

        claims.Should().Contain(claim =>
            claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == userId.ToString());
    }

    [Fact]
    public void GenerateAccessToken_IncludesNameClaim()
    {
        // Arrange
        const string userName = "+15559876543";
        const string displayName = "Dr. John Doe";
        var request = JwtTestDataBuilder.CreateRequest(userName: userName, name: displayName);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        var claims = JwtTokenTestHelper.GetClaims(result.AccessToken);

        claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.Name && claim.Value == userName);

        claims.Should().Contain(claim =>
            claim.Type == "display_name" && claim.Value == displayName);
    }

    [Fact]
    public void GenerateAccessToken_IncludesRoleClaims()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest(roles: ["Doctor", "Admin"]);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        JwtTokenTestHelper.GetRoleClaims(result.AccessToken)
            .Should().BeEquivalentTo(["Doctor", "Admin"]);
    }

    [Fact]
    public void GenerateAccessToken_IncludesAdminRole()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest(roles: [nameof(UserRole.Admin)]);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        JwtTokenTestHelper.GetRoleClaims(result.AccessToken)
            .Should().Contain(nameof(UserRole.Admin));
    }

    [Fact]
    public void GenerateAccessToken_IncludesDoctorRole()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest(roles: [nameof(UserRole.Doctor)]);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        JwtTokenTestHelper.GetRoleClaims(result.AccessToken)
            .Should().Contain(nameof(UserRole.Doctor));
    }

    [Fact]
    public void GenerateAccessToken_IncludesPatientRole()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest(roles: [nameof(UserRole.Patient)]);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        JwtTokenTestHelper.GetRoleClaims(result.AccessToken)
            .Should().Contain(nameof(UserRole.Patient));
    }

    [Fact]
    public void GenerateAccessToken_DeduplicatesRoleClaims()
    {
        // Arrange
        var request = JwtTestDataBuilder.CreateRequest(roles: ["Doctor", "doctor", "DOCTOR"]);
        var sut = CreateSut();

        // Act
        var result = sut.GenerateAccessToken(request);

        // Assert
        JwtTokenTestHelper.GetRoleClaims(result.AccessToken)
            .Should().ContainSingle()
            .Which.Should().Be("Doctor");
    }
}
