using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Common;
using Phisio.Application.Patients;

namespace Phisio.Tests.Api.Controllers;

public class PatientsControllerGetPatientsTests
{
    [Fact]
    public async Task GetPatients_WhenPatientsExist_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patients = new List<PatientDto>
        {
            new(
                Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                "Alice Patient",
                "+15551111111",
                new DateTime(2024, 1, 10, 10, 0, 0, DateTimeKind.Utc))
        };

        var patientService = new Mock<IPatientService>();
        patientService.Setup(service => service.GetPatientsAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<PatientDto>>.Success(patients));

        var controller = PatientsControllerTestHelper.CreateController(
            patientService,
            PatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatients(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(patients);
    }

    [Fact]
    public async Task GetPatients_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var patientService = new Mock<IPatientService>();
        var controller = PatientsControllerTestHelper.CreateController(patientService);

        // Act
        var result = await controller.GetPatients(CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        patientService.Verify(
            service => service.GetPatientsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class PatientsControllerGetPatientTests
{
    [Fact]
    public async Task GetPatient_WhenPatientExists_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var patient = new PatientDto(
            patientId,
            "Alice Patient",
            "+15551111111",
            new DateTime(2024, 1, 10, 10, 0, 0, DateTimeKind.Utc));

        var patientService = new Mock<IPatientService>();
        patientService.Setup(service => service.GetPatientByIdAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Success(patient));

        var controller = PatientsControllerTestHelper.CreateController(
            patientService,
            PatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatient(patientId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(patient);
    }

    [Fact]
    public async Task GetPatient_WhenPatientNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var patientService = new Mock<IPatientService>();
        patientService.Setup(service => service.GetPatientByIdAsync(doctorId, patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Failure(["Patient not found."]));

        var controller = PatientsControllerTestHelper.CreateController(
            patientService,
            PatientsControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetPatient(patientId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetPatient_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        // Arrange
        var patientId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var patientService = new Mock<IPatientService>();
        var controller = PatientsControllerTestHelper.CreateController(patientService);

        // Act
        var result = await controller.GetPatient(patientId, CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        patientService.Verify(
            service => service.GetPatientByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
