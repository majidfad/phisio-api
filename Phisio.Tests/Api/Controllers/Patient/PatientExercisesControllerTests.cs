using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Common;
using Phisio.Application.PatientExercises;
using Phisio.Api.Controllers.Patient;
using System.Security.Claims;

namespace Phisio.Tests.Api.Controllers.Patient;

public class PatientExercisesControllerTests
{
    [Fact]
    public async Task GetExercises_WhenPatientHasAssignments_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var userExerciseId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var exerciseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var response = new PatientExercisesResponse(
            "دکتر رحمانی",
            [
                new PatientExerciseItemDto(
                    userExerciseId,
                    exerciseId,
                    "کشش گردن",
                    "/uploads/exercises/neck.mp4",
                    DateTime.UtcNow,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    true)
            ]);

        var patientExerciseService = new Mock<IPatientExerciseService>();
        patientExerciseService
            .Setup(service => service.GetExercisesAsync(patientId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientExercisesResponse>.Success(response));

        var controller = CreateController(patientExerciseService, patientId);

        // Act
        var result = await controller.GetExercises(null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetExercises_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var patientExerciseService = new Mock<IPatientExerciseService>();
        var controller = CreateController(patientExerciseService, userId: null);

        // Act
        var result = await controller.GetExercises(null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();

        patientExerciseService.Verify(
            service => service.GetExercisesAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteExercises_WhenCompletionSucceeds_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var userExerciseId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var request = new CompleteExercisesRequest { UserExerciseIds = [userExerciseId] };
        var response = new CompleteExercisesResponse(
            DateOnly.FromDateTime(DateTime.UtcNow),
            [userExerciseId],
            []);

        var patientExerciseService = new Mock<IPatientExerciseService>();
        patientExerciseService
            .Setup(service => service.CompleteExercisesAsync(patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<CompleteExercisesResponse>.Success(response));

        var controller = CreateController(patientExerciseService, patientId);

        // Act
        var result = await controller.CompleteExercises(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task CompleteExercises_WhenAssignmentNotFound_ReturnsNotFound()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new CompleteExercisesRequest
        {
            UserExerciseIds = [Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890")],
        };

        var patientExerciseService = new Mock<IPatientExerciseService>();
        patientExerciseService
            .Setup(service => service.CompleteExercisesAsync(patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<CompleteExercisesResponse>.Failure([PatientExerciseErrors.AssignmentNotFound]));

        var controller = CreateController(patientExerciseService, patientId);

        // Act
        var result = await controller.CompleteExercises(request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetTodayExercises_WhenPatientHasTodayAssignments_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var userExerciseId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var exerciseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new PatientTodayExercisesResponse(
        [
            new PatientDoctorExerciseGroupDto(
                "دکتر رحمانی",
                [
                    new PatientTodayExerciseItemDto(
                        userExerciseId,
                        exerciseId,
                        "کشش گردن",
                        "/uploads/exercises/neck.mp4",
                        today,
                        false),
                ]),
        ]);

        var patientExerciseService = new Mock<IPatientExerciseService>();
        patientExerciseService
            .Setup(service => service.GetTodayExercisesAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientTodayExercisesResponse>.Success(response));

        var controller = CreateController(patientExerciseService, patientId);

        // Act
        var result = await controller.GetTodayExercises(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetTodayExercises_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var patientExerciseService = new Mock<IPatientExerciseService>();
        var controller = CreateController(patientExerciseService, userId: null);

        // Act
        var result = await controller.GetTodayExercises(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();

        patientExerciseService.Verify(
            service => service.GetTodayExercisesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PatientExercisesController CreateController(
        Mock<IPatientExerciseService> patientExerciseService,
        Guid? userId)
    {
        ClaimsPrincipal user = userId is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                authenticationType: "Test"));

        return new PatientExercisesController(patientExerciseService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }
}
