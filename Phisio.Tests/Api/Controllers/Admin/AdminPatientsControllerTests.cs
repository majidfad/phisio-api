using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Admin.Patients;
using Phisio.Application.Common;
using Phisio.Application.Patients;

namespace Phisio.Tests.Api.Controllers.Admin;

public class AdminPatientsControllerGetPatientsTests
{
    [Fact]
    public async Task GetPatients_WhenPatientsExist_ReturnsOk()
    {
        // Arrange
        var patients = new List<PatientDto>
        {
            new(
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Alice Patient",
                "+15551111111",
                DateTime.UtcNow.AddDays(-2),
                null,
                DateTime.UtcNow.AddDays(-5))
        };

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<PatientDto>>.Success(patients));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.GetPatients(cancellationToken: CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(patients);
    }
}

public class AdminPatientsControllerGetPatientTests
{
    [Fact]
    public async Task GetPatient_WhenPatientExists_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var patient = new PatientDto(
            patientId,
            "Alice Patient",
            "+15551111111",
            DateTime.UtcNow.AddDays(-2),
            null,
            DateTime.UtcNow.AddDays(-5));

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.GetByIdAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Success(patient));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

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
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.GetByIdAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Failure(["Patient not found."]));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.GetPatient(patientId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AdminPatientsControllerCreatePatientTests
{
    [Fact]
    public async Task CreatePatient_WhenCreationSucceeds_ReturnsCreated()
    {
        // Arrange
        var request = new CreateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111"
        };

        var createdPatient = new PatientDto(
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            request.Name,
            request.PhoneNumber,
            DateTime.UtcNow,
            null,
            DateTime.UtcNow);

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Success(createdPatient));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.CreatePatient(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(createdPatient);
    }

    [Fact]
    public async Task CreatePatient_WhenCreationFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateAdminPatientDto
        {
            Name = "Alice Patient",
            PhoneNumber = "+15551111111"
        };

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Failure(["Phone number is already registered."]));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.CreatePatient(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

public class AdminPatientsControllerUpdatePatientTests
{
    [Fact]
    public async Task UpdatePatient_WhenUpdateSucceeds_ReturnsOk()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new UpdateAdminPatientDto
        {
            Name = "Alice Updated",
            PhoneNumber = "+15552222222",
            Email = "alice@example.com"
        };

        var updatedPatient = new PatientDto(
            patientId,
            request.Name,
            request.PhoneNumber,
            DateTime.UtcNow.AddDays(-2),
            request.Email,
            DateTime.UtcNow.AddDays(-5));

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.UpdateAsync(patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Success(updatedPatient));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.UpdatePatient(patientId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(updatedPatient);
    }

    [Fact]
    public async Task UpdatePatient_WhenPatientNotFound_ReturnsNotFound()
    {
        // Arrange
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var request = new UpdateAdminPatientDto
        {
            Name = "Alice Updated",
            PhoneNumber = "+15552222222"
        };

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.UpdateAsync(patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Failure(["Patient not found."]));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.UpdatePatient(patientId, request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdatePatient_WhenValidationFails_ReturnsBadRequest()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new UpdateAdminPatientDto
        {
            Name = "Alice Updated",
            PhoneNumber = "+15552222222"
        };

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.UpdateAsync(patientId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDto>.Failure(["Phone number is already registered."]));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.UpdatePatient(patientId, request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

public class AdminPatientsControllerDeletePatientTests
{
    [Fact]
    public async Task DeletePatient_WhenDeletionSucceeds_ReturnsNoContent()
    {
        // Arrange
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.DeleteAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.DeletePatient(patientId, CancellationToken.None);

        // Assert
        var noContentResult = result.Should().BeOfType<NoContentResult>().Subject;
        noContentResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task DeletePatient_WhenPatientNotFound_ReturnsNotFound()
    {
        // Arrange
        var patientId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var adminPatientService = new Mock<IAdminPatientService>();
        adminPatientService.Setup(service => service.DeleteAsync(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure(["Patient not found."]));

        var controller = AdminPatientsControllerTestHelper.CreateController(adminPatientService);

        // Act
        var result = await controller.DeletePatient(patientId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
