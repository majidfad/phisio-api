using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Auth;
using Phisio.Application.Common;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Api.Controllers;

public class AuthControllerRegisterTests
{
    [Fact]
    public async Task Register_WhenRegistrationSucceeds_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var response = new RegisterResponse(
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            "+989121234567",
            request.Name,
            UserRole.Patient,
            RegisterMessages.PatientRegistered);

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<RegisterResponse>.Success(response));

        var controller = AuthControllerTestHelper.CreateController(authService);

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Register_WhenRegistrationFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<RegisterResponse>.Failure([AuthErrorMessages.DuplicatePhoneNumber]));

        var controller = AuthControllerTestHelper.CreateController(authService);

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

public class AuthControllerLoginTests
{
    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsOk()
    {
        // Arrange
        var request = new LoginRequest
        {
            PhoneNumber = "+15551234567",
            Password = "SecurePass1!"
        };

        var response = new AuthResponse(
            "test-access-token",
            DateTime.UtcNow.AddHours(1),
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            request.PhoneNumber,
            "jane.smith@example.com",
            "Dr. Jane Smith",
            UserRole.Doctor);

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<AuthResponse>.Success(response));

        var controller = AuthControllerTestHelper.CreateController(authService);

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            PhoneNumber = "+15551234567",
            Password = "WrongPass1!"
        };

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<AuthResponse>.Failure(["Invalid phone number or password."]));

        var controller = AuthControllerTestHelper.CreateController(authService);

        // Act
        var result = await controller.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}

public class AuthControllerMeTests
{
    [Fact]
    public async Task Me_WhenUserExists_ReturnsOk()
    {
        // Arrange
        var userId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var response = new MeResponse(
            userId,
            "+15551234567",
            "jane.smith@example.com",
            ["Doctor"]);

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.GetCurrentUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<MeResponse>.Success(response));

        var controller = AuthControllerTestHelper.CreateController(
            authService,
            AuthControllerTestHelper.CreateAuthenticatedUser(userId));

        // Act
        var result = await controller.Me(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Me_WhenAuthenticationTokenIsInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var authService = new Mock<IAuthService>();
        var controller = AuthControllerTestHelper.CreateController(
            authService,
            AuthControllerTestHelper.CreateUserWithInvalidIdClaim());

        // Act
        var result = await controller.Me(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        authService.Verify(
            service => service.GetCurrentUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Me_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.GetCurrentUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<MeResponse>.Failure(["User not found."]));

        var controller = AuthControllerTestHelper.CreateController(
            authService,
            AuthControllerTestHelper.CreateAuthenticatedUser(userId));

        // Act
        var result = await controller.Me(CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AuthControllerRegisterPatientTests
{
    [Fact]
    public async Task RegisterPatient_WhenRegistrationSucceeds_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterPatientRequest
        {
            Name = "Alice Patient",
            PhoneNumber = "+15559876543",
            Password = "SecurePass1!"
        };

        var response = new RegisterPatientResponse(
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            request.PhoneNumber,
            request.Name,
            UserRole.Patient);

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.RegisterPatientAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<RegisterPatientResponse>.Success(response));

        var controller = AuthControllerTestHelper.CreateController(authService);

        // Act
        var result = await controller.RegisterPatient(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task RegisterPatient_WhenRegistrationFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterPatientRequest
        {
            Name = "Alice Patient",
            PhoneNumber = "+15559876543",
            Password = "SecurePass1!"
        };

        var authService = new Mock<IAuthService>();
        authService.Setup(service => service.RegisterPatientAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<RegisterPatientResponse>.Failure([AuthErrorMessages.DuplicatePhoneNumber]));

        var controller = AuthControllerTestHelper.CreateController(authService);

        // Act
        var result = await controller.RegisterPatient(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
