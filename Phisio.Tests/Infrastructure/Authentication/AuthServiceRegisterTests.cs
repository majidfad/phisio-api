using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Phisio.Application.Auth;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;
using Phisio.Tests.TestHelpers;

namespace Phisio.Tests.Infrastructure.Authentication;

public class AuthServiceRegisterTests
{
    [Fact]
    public async Task RegisterAsync_WhenRequestIsValid_ReturnsSuccess()
    {
        // Arrange
        var request = RegisterRequestBuilder.Valid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Patient)))
            .ReturnsAsync(IdentityResult.Success);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be(request.Name);
        result.Value.Role.Should().Be(UserRole.Patient);

        userManager.Verify(
            manager => manager.CreateAsync(
                It.Is<ApplicationUser>(user =>
                    user.Name == request.Name
                    && user.Role == UserRole.Patient
                    && user.IsEnabled
                    && user.UserName == UserCredentials.NormalizePhone(request.PhoneNumber)),
                request.Password),
            Times.Once);

        userManager.Verify(
            manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Patient)),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenPhoneNumberAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var request = RegisterRequestBuilder.Valid();
        var normalizedPhone = UserCredentials.NormalizePhone(request.PhoneNumber);
        var existingUser = ApplicationUserBuilder.Patient(phoneNumber: normalizedPhone);
        var userManager = IdentityMockFactory.CreateUserManager([existingUser]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(AuthErrorMessages.DuplicatePhoneNumber);

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordsDoNotMatch_ReturnsFailure()
    {
        // Arrange
        var request = RegisterRequestBuilder.Valid();
        request.ConfirmPassword = "DifferentPass1!";
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();
        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(AuthErrorMessages.PasswordMismatch);

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenIdentityCreateFails_ReturnsLocalizedFailure()
    {
        // Arrange
        var request = RegisterRequestBuilder.Valid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 8 characters." }));

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("رمز عبور باید حداقل ۸ کاراکتر باشد.");

        userManager.Verify(
            manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenPatientRoleCannotBeCreated_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = RegisterRequestBuilder.Valid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(false);

        roleManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid role." }));

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var act = () => sut.RegisterAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to create role 'Patient'*");

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_AfterSuccessfulRegistration_ReturnsSuccess()
    {
        // Arrange
        const string password = "Password123!";
        var request = RegisterRequestBuilder.Valid();
        var users = new List<ApplicationUser>();
        var userManager = IdentityMockFactory.CreateUserManager();
        userManager.Setup(manager => manager.Users).Returns(() => users.AsAsyncQueryable());

        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create("jwt-token", DateTime.UtcNow.AddHours(2));

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), password))
            .Callback<ApplicationUser, string>((user, _) => users.Add(user))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Patient)))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.CheckPasswordAsync(It.IsAny<ApplicationUser>(), password))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(["Patient"]);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var registerResult = await sut.RegisterAsync(request);
        var loginResult = await sut.LoginAsync(new LoginRequest
        {
            PhoneNumber = request.PhoneNumber,
            Password = password
        });

        // Assert
        registerResult.Succeeded.Should().BeTrue();
        loginResult.Succeeded.Should().BeTrue();
        loginResult.Value.Should().NotBeNull();
        loginResult.Value!.Role.Should().Be(UserRole.Patient);
        loginResult.Value.AccessToken.Should().Be("jwt-token");
    }
}
