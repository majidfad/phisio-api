using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Phisio.Application.Auth;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Authentication;

public class AuthServiceChangePasswordTests
{
    private static ChangePasswordRequest ValidRequest() =>
        new()
        {
            CurrentPassword = "OldPassword1!",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "NewPassword1!"
        };

    [Fact]
    public async Task ChangePasswordAsync_WhenRequestIsValid_ReturnsSuccessMessage()
    {
        // Arrange
        var user = ApplicationUserBuilder.Patient();
        var request = ValidRequest();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        userManager.Setup(manager =>
                manager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.ChangePasswordAsync(user.Id, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Message.Should().Be(AuthMessages.PasswordChanged);

        userManager.Verify(
            manager => manager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword),
            Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenNewPasswordsDoNotMatch_ReturnsFailure()
    {
        // Arrange
        var request = ValidRequest();
        request.ConfirmPassword = "Different1!";

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();
        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.ChangePasswordAsync(Guid.NewGuid(), request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(AuthErrorMessages.PasswordMismatch);

        userManager.Verify(
            manager => manager.ChangePasswordAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenNewPasswordEqualsCurrent_ReturnsFailure()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "SamePassword1!",
            NewPassword = "SamePassword1!",
            ConfirmPassword = "SamePassword1!"
        };

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();
        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.ChangePasswordAsync(Guid.NewGuid(), request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(AuthErrorMessages.NewPasswordSameAsCurrent);

        userManager.Verify(
            manager => manager.ChangePasswordAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = ValidRequest();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.ChangePasswordAsync(userId, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("User not found.");
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenCurrentPasswordIsWrong_ReturnsLocalizedIdentityError()
    {
        // Arrange
        var user = ApplicationUserBuilder.Patient();
        var request = ValidRequest();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        userManager.Setup(manager =>
                manager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordMismatch",
                Description = "Incorrect password."
            }));

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.ChangePasswordAsync(user.Id, request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("رمز عبور فعلی نادرست است.");
    }
}
