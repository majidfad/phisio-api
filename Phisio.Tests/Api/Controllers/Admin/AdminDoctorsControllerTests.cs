using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Common;
using Phisio.Application.Doctors;

namespace Phisio.Tests.Api.Controllers.Admin;

internal static class AdminDoctorsControllerTestHelper
{
    public static AdminDoctorsController CreateController(Mock<IAdminDoctorService> adminDoctorService)
    {
        return new AdminDoctorsController(adminDoctorService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}

public class AdminDoctorsControllerGetDoctorsTests
{
    [Fact]
    public async Task GetDoctors_WhenDoctorsExist_ReturnsOk()
    {
        // Arrange
        var doctors = new List<DoctorDto>
        {
            new(
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Dr. Alice",
                "+15551111111",
                "Orthopedics",
                "MD-11111",
                "Clinic A",
                DateTime.UtcNow.AddDays(-5))
        };

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorDto>>.Success(doctors));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.GetDoctors(cancellationToken: CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(doctors);
    }
}

public class AdminDoctorsControllerGetDoctorTests
{
    [Fact]
    public async Task GetDoctor_WhenDoctorExists_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctor = new DoctorDto(
            doctorId,
            "Dr. Alice",
            "+15551111111",
            "Orthopedics",
            "MD-11111",
            "Clinic A",
            DateTime.UtcNow.AddDays(-5));

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDto>.Success(doctor));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.GetDoctor(doctorId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(doctor);
    }

    [Fact]
    public async Task GetDoctor_WhenDoctorNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.GetByIdAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDto>.Failure(["Doctor not found."]));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.GetDoctor(doctorId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AdminDoctorsControllerCreateDoctorTests
{
    [Fact]
    public async Task CreateDoctor_WhenCreationSucceeds_ReturnsCreated()
    {
        // Arrange
        var request = new CreateAdminDoctorDto
        {
            Name = "Dr. Alice",
            PhoneNumber = "+15551111111",
            Specialty = "Orthopedics",
            MedicalLicenseNumber = "MD-11111",
            ClinicAddress = "Clinic A"
        };

        var createdDoctor = new DoctorDto(
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            request.Name,
            request.PhoneNumber,
            request.Specialty,
            request.MedicalLicenseNumber,
            request.ClinicAddress,
            DateTime.UtcNow);

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDto>.Success(createdDoctor));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.CreateDoctor(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(createdDoctor);
    }

    [Fact]
    public async Task CreateDoctor_WhenCreationFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateAdminDoctorDto
        {
            Name = "Dr. Alice",
            PhoneNumber = "+15551111111",
            Specialty = "Orthopedics",
            MedicalLicenseNumber = "MD-11111",
            ClinicAddress = "Clinic A"
        };

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDto>.Failure(["Phone number is already registered."]));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.CreateDoctor(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

public class AdminDoctorsControllerUpdateDoctorTests
{
    [Fact]
    public async Task UpdateDoctor_WhenUpdateSucceeds_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new UpdateAdminDoctorDto
        {
            Name = "Dr. Updated",
            PhoneNumber = "+15552222222",
            Email = "alice@example.com",
            Specialty = "Physiotherapy",
            MedicalLicenseNumber = "MD-22222",
            ClinicAddress = "Clinic B"
        };

        var updatedDoctor = new DoctorDto(
            doctorId,
            request.Name,
            request.PhoneNumber,
            request.Specialty,
            request.MedicalLicenseNumber,
            request.ClinicAddress,
            DateTime.UtcNow.AddDays(-5),
            request.Email);

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.UpdateAsync(doctorId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDto>.Success(updatedDoctor));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.UpdateDoctor(doctorId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(updatedDoctor);
    }

    [Fact]
    public async Task UpdateDoctor_WhenDoctorNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var request = new UpdateAdminDoctorDto
        {
            Name = "Dr. Updated",
            PhoneNumber = "+15552222222",
            Specialty = "Physiotherapy",
            MedicalLicenseNumber = "MD-22222",
            ClinicAddress = "Clinic B"
        };

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.UpdateAsync(doctorId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDto>.Failure(["Doctor not found."]));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.UpdateDoctor(doctorId, request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AdminDoctorsControllerDeleteDoctorTests
{
    [Fact]
    public async Task DeleteDoctor_WhenDeleteSucceeds_ReturnsNoContent()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.DeleteAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.DeleteDoctor(doctorId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteDoctor_WhenDoctorNotFound_ReturnsNotFound()
    {
        // Arrange
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.DeleteAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure(["Doctor not found."]));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.DeleteDoctor(doctorId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AdminDoctorsControllerActivateDoctorTests
{
    [Fact]
    public async Task ActivateDoctor_WhenActivationSucceeds_ReturnsNoContent()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var adminDoctorService = new Mock<IAdminDoctorService>();
        adminDoctorService.Setup(service => service.ActivateAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = AdminDoctorsControllerTestHelper.CreateController(adminDoctorService);

        // Act
        var result = await controller.ActivateDoctor(doctorId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}
