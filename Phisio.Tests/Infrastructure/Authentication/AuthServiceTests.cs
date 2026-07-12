using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Phisio.Application.Auth;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Identity;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Authentication;

public class AuthServiceLoginTests
{
    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsSuccess()
    {
        // Arrange
        const string password = "SecurePass1!";
        var user = ApplicationUserBuilder.Doctor();
        var request = new LoginRequest
        {
            PhoneNumber = user.PhoneNumber!,
            Password = password
        };

        var userManager = IdentityMockFactory.CreateUserManager([user]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create("jwt-token", DateTime.UtcNow.AddHours(2));

        userManager.Setup(manager => manager.CheckPasswordAsync(user, password))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(["Doctor"]);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.LoginAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be("jwt-token");
        result.Value.PhoneNumber.Should().Be(user.PhoneNumber);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Name.Should().Be(user.Name);
        result.Value.Role.Should().Be(UserRole.Doctor);

        jwtTokenService.Verify(
            service => service.GenerateAccessToken(
                It.Is<AccessTokenGenerationRequest>(tokenRequest =>
                    tokenRequest.UserId == user.Id
                    && tokenRequest.UserName == user.UserName
                    && tokenRequest.Name == user.Name)),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new LoginRequest
        {
            PhoneNumber = "+19999999999",
            Password = "SecurePass1!"
        };

        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();
        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.LoginAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Invalid phone number or password.");

        userManager.Verify(
            manager => manager.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);

        jwtTokenService.Verify(
            service => service.GenerateAccessToken(It.IsAny<AccessTokenGenerationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_ReturnsFailure()
    {
        // Arrange
        var user = ApplicationUserBuilder.Doctor();
        var request = new LoginRequest
        {
            PhoneNumber = user.PhoneNumber!,
            Password = "WrongPass1!"
        };

        var userManager = IdentityMockFactory.CreateUserManager([user]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        userManager.Setup(manager => manager.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(false);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.LoginAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Invalid phone number or password.");

        jwtTokenService.Verify(
            service => service.GenerateAccessToken(It.IsAny<AccessTokenGenerationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsDisabled_ReturnsFailure()
    {
        // Arrange
        var user = ApplicationUserBuilder.Doctor();
        user.IsEnabled = false;
        var request = new LoginRequest
        {
            PhoneNumber = user.PhoneNumber!,
            Password = "SecurePass1!"
        };

        var userManager = IdentityMockFactory.CreateUserManager([user]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();
        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.LoginAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("This account has been disabled.");

        userManager.Verify(
            manager => manager.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("989129998877")]
    [InlineData("+98 912 999 8877")]
    [InlineData("  +989129998877  ")]
    public async Task LoginAsync_WhenPhoneNumberFormatDiffersFromStoredValue_ReturnsSuccess(string loginPhoneNumber)
    {
        // Arrange
        const string password = "SecurePass1!";
        var user = ApplicationUserBuilder.Doctor(phoneNumber: "+989129998877");
        var request = new LoginRequest
        {
            PhoneNumber = loginPhoneNumber,
            Password = password
        };

        var userManager = IdentityMockFactory.CreateUserManager([user]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create("jwt-token", DateTime.UtcNow.AddHours(2));

        userManager.Setup(manager => manager.CheckPasswordAsync(user, password))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(["Doctor"]);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.LoginAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PhoneNumber.Should().Be(user.PhoneNumber);
    }
}

public class AuthServiceMeTests
{
    [Fact]
    public async Task GetCurrentUserAsync_WhenUserExists_ReturnsSuccess()
    {
        // Arrange
        var user = ApplicationUserBuilder.Doctor();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(user.Id.ToString()))
            .ReturnsAsync(user);

        userManager.Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(["Doctor", "Admin"]);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.GetCurrentUserAsync(user.Id);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(user.Id);
        result.Value.PhoneNumber.Should().Be(user.PhoneNumber);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Roles.Should().BeEquivalentTo(["Doctor", "Admin"]);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        userManager.Setup(manager => manager.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.GetCurrentUserAsync(userId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("User not found.");

        userManager.Verify(manager => manager.GetRolesAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}

public class AuthServiceRegisterPatientTests
{
    [Fact]
    public async Task RegisterPatientAsync_WhenRequestIsValid_ReturnsSuccess()
    {
        // Arrange
        var request = RegisterPatientRequestBuilder.Valid();
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
        var result = await sut.RegisterPatientAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PhoneNumber.Should().Be(request.PhoneNumber);
        result.Value.Name.Should().Be(request.Name);
        result.Value.Role.Should().Be(UserRole.Patient);

        userManager.Verify(
            manager => manager.CreateAsync(
                It.Is<ApplicationUser>(user =>
                    user.Name == request.Name
                    && user.Role == UserRole.Patient
                    && user.PhoneNumber == request.PhoneNumber),
                request.Password),
            Times.Once);
    }

    [Fact]
    public async Task RegisterPatientAsync_WhenPhoneNumberAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var request = RegisterPatientRequestBuilder.Valid();
        var existingUser = ApplicationUserBuilder.Patient(phoneNumber: request.PhoneNumber);
        var userManager = IdentityMockFactory.CreateUserManager([existingUser]);
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterPatientAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(AuthErrorMessages.DuplicatePhoneNumber);

        userManager.Verify(
            manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterPatientAsync_WhenIdentityCreateFails_ReturnsFailure()
    {
        // Arrange
        var request = RegisterPatientRequestBuilder.Valid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password is too weak." }));

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterPatientAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Password is too weak.");
    }

    [Fact]
    public async Task RegisterPatientAsync_WhenRoleAssignmentFails_DeletesUserAndReturnsFailure()
    {
        // Arrange
        var request = RegisterPatientRequestBuilder.Valid();
        var userManager = IdentityMockFactory.CreateUserManager();
        var roleManager = IdentityMockFactory.CreateRoleManager();
        var jwtTokenService = JwtTokenServiceMockFactory.Create();

        roleManager.Setup(manager => manager.RoleExistsAsync(nameof(UserRole.Patient)))
            .ReturnsAsync(true);

        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(UserRole.Patient)))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));

        userManager.Setup(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = new AuthService(userManager.Object, roleManager.Object, jwtTokenService.Object);

        // Act
        var result = await sut.RegisterPatientAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Role assignment failed.");

        userManager.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Once);
    }
}
