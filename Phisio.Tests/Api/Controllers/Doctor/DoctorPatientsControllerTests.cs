using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Doctor;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Api.Controllers.Doctor;

internal static class DoctorPatientsControllerTestHelper
{
    public static DoctorPatientsController CreateController(
        Mock<IDoctorPatientService> doctorPatientService,
        ClaimsPrincipal? user = null)
    {
        return new DoctorPatientsController(doctorPatientService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    public static ClaimsPrincipal CreateAuthenticatedDoctor(Guid doctorId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, doctorId.ToString())],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}

public class DoctorPatientsControllerGetPatientsTests
{
    [Fact]
    public async Task GetPatients_WhenDoctorIsAuthenticated_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patients = new List<DoctorPatientDto>
        {
            new(
                Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
                "Alice Patient",
                "+15551111111",
                DateTime.UtcNow.AddDays(-2))
        };

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.GetPatientsAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorPatientDto>>.Success(patients));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatients(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(patients);
    }
}

public class DoctorPatientsControllerApproveRequestTests
{
    [Fact]
    public async Task ApproveRequest_WhenSucceeded_ReturnsOk()
    {
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var approved = new DoctorPatientDto(
            patientId,
            "Alice Patient",
            "+15551111111",
            DateTime.UtcNow);

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.ApproveRequestAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorPatientDto>.Success(approved));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        var result = await controller.ApproveRequest(patientId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(approved);
    }

    [Fact]
    public async Task ApproveRequest_WhenRequestMissing_ReturnsNotFound()
    {
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.ApproveRequestAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorPatientDto>.Failure([DoctorPatientErrors.RequestNotFound]));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        var result = await controller.ApproveRequest(patientId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class DoctorPatientsControllerRemovePatientTests
{
    [Fact]
    public async Task RemovePatient_WhenRelationshipRemoved_ReturnsNoContent()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.RemoveAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.RemovePatient(patientId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}

public class DoctorPatientsControllerGetPatientExercisesTests
{
    [Fact]
    public async Task GetPatientExercises_WhenExercisesExist_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var exercises = new List<DoctorPatientExerciseDto>
        {
            new(
                Guid.Parse("c1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Hamstring Stretch",
                "https://example.com/hamstring.mp4",
                ExerciseMediaType.UploadedVideo,
                DateTime.UtcNow.AddDays(-2),
                DateOnly.FromDateTime(DateTime.UtcNow),
                Sets: 3,
                Reps: "10",
                HoldSeconds: null,
                RestSeconds: null,
                Side: ExerciseSide.NotApplicable,
                ClinicianNote: null,
                PatientCue: null)
        };

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.GetPatientExercisesAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Success(exercises));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatientExercises(patientId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(exercises);
    }

    [Fact]
    public async Task GetPatientExercises_WhenPatientNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.GetPatientExercisesAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>.Failure(["بیمار یافت نشد"]));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatientExercises(patientId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

public class DoctorPatientsControllerAssignPatientExercisesTests
{
    [Fact]
    public async Task AssignPatientExercises_WhenAssignmentsCreated_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var exerciseId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var request = new AssignPatientExercisesRequest(
            [new AssignPatientExerciseItem(
                exerciseId, Sets: 3, Reps: "10", HoldSeconds: null, RestSeconds: null,
                Side: ExerciseSide.NotApplicable, ClinicianNote: null, PatientCue: null)],
            [DateOnly.FromDateTime(DateTime.UtcNow)]);
        var response = new AssignPatientExercisesResultDto(1);

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.AssignExercisesAsync(doctorId, patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<AssignPatientExercisesResultDto>.Success(response));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.AssignPatientExercises(patientId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }
}

public class DoctorPatientsControllerGetPatientExerciseHistoryTests
{
    [Fact]
    public async Task GetPatientExerciseHistory_WhenHistoryExists_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new PatientExerciseHistoryResponse(
            new PatientExerciseHistoryPatientDto(patientId, "Alice Patient", "+15551111111"),
            new PatientExerciseHistorySummaryDto(2, 1, 0, 100),
            [
                new PatientExerciseHistoryDayDto(
                    today,
                    2,
                    true,
                    [
                        new PatientExerciseHistoryExerciseDto(
                            Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                            Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                            "Neck Stretch",
                            true,
                            3,
                            "10",
                            5,
                            30,
                            Phisio.Domain.Enums.ExerciseSide.Left,
                            "Watch form",
                            "Keep spine neutral"),
                    ],
                    4,
                    3,
                    "امروز درد زانو کمتر بود."),
            ],
            TotalDays: 1,
            Page: 1,
            PageSize: 10);

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.GetExerciseHistoryAsync(
                doctorId,
                patientId,
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientExerciseHistoryResponse>.Success(response));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatientExerciseHistory(patientId, 1, 10, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetPatientExerciseHistory_WhenPatientNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(service => service.GetExerciseHistoryAsync(
                doctorId,
                patientId,
                1,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientExerciseHistoryResponse>.Failure(["بیمار یافت نشد"]));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatientExerciseHistory(patientId, 1, 10, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

public class DoctorPatientsControllerDeleteProgramTests
{
    [Fact]
    public async Task DeletePatientProgram_WhenSucceeded_ReturnsNoContent()
    {
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var programId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(s => s.DeleteProgramAsync(
                doctorId, patientId, programId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        var result = await controller.DeletePatientProgram(patientId, programId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeletePatientProgram_WhenNotFound_ReturnsNotFound()
    {
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var programId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(s => s.DeleteProgramAsync(
                doctorId, patientId, programId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure([DoctorPatientErrors.ProgramNotFound]));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        var result = await controller.DeletePatientProgram(patientId, programId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

public class DoctorPatientsControllerExerciseStatsTests
{
    [Fact]
    public async Task GetPatientExerciseStats_WhenSucceeded_ReturnsOk()
    {
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new PatientExerciseStatsResponse(
            from,
            to,
            new PatientExerciseStatsSummaryDto(2, 1, 1, 50, 4, 2, 50, 4.0, 3.0, 1),
            [],
            [],
            []);

        var doctorPatientService = new Mock<IDoctorPatientService>();
        doctorPatientService.Setup(s => s.GetExerciseStatsAsync(
                doctorId, patientId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientExerciseStatsResponse>.Success(response));

        var controller = DoctorPatientsControllerTestHelper.CreateController(
            doctorPatientService,
            DoctorPatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        var result = await controller.GetPatientExerciseStats(patientId, from, to, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }
}

