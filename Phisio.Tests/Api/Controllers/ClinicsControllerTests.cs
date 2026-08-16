using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Clinics;
using Phisio.Application.Common;

namespace Phisio.Tests.Api.Controllers;

internal static class ClinicsControllerTestHelper
{
    public static ClinicsController CreateController(
        Mock<IClinicService> clinicService,
        ClaimsPrincipal? user = null,
        Mock<IAdminDoctorService>? doctorService = null)
    {
        return new ClinicsController(
            clinicService.Object,
            (doctorService ?? new Mock<IAdminDoctorService>()).Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };
    }

    public static ClaimsPrincipal CreateClinicManager(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.ClinicManager),
            ],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal CreateAdmin(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Admin),
            ],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}

public class ClinicsControllerCreateTests
{
    [Fact]
    public async Task CreateClinic_WhenSucceeded_ReturnsCreated()
    {
        var managerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new CreateClinicDto
        {
            Name = "Central Clinic",
            Address = "123 Main St",
            PhoneNumbers = ["+15551111111"],
        };

        var clinic = new ClinicDto(
            Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
            request.Name,
            request.Address,
            managerId,
            request.PhoneNumbers.ToList(),
            DateTime.UtcNow);

        var clinicService = new Mock<IClinicService>();
        clinicService.Setup(service => service.CreateAsync(
                It.Is<ClinicAccessContext>(access =>
                    access.UserId == managerId && access.IsAdmin == false),
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicDto>.Success(clinic));

        var controller = ClinicsControllerTestHelper.CreateController(
            clinicService,
            ClinicsControllerTestHelper.CreateClinicManager(managerId));

        var result = await controller.CreateClinic(request, CancellationToken.None);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(clinic);
    }

    [Fact]
    public async Task CreateClinic_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        var clinicService = new Mock<IClinicService>();
        var controller = ClinicsControllerTestHelper.CreateController(clinicService);

        var result = await controller.CreateClinic(
            new CreateClinicDto { Name = "X", Address = "Y", PhoneNumbers = ["+15551111111"] },
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }
}

public class ClinicsControllerGetTests
{
    [Fact]
    public async Task GetClinic_WhenNotFound_ReturnsNotFound()
    {
        var managerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var clinicId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var clinicService = new Mock<IClinicService>();
        clinicService.Setup(service => service.GetByIdAsync(
                It.IsAny<ClinicAccessContext>(),
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicDto>.Failure([ClinicErrors.NotFound]));

        var controller = ClinicsControllerTestHelper.CreateController(
            clinicService,
            ClinicsControllerTestHelper.CreateClinicManager(managerId));

        var result = await controller.GetClinic(clinicId, CancellationToken.None);

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class ClinicsControllerDeleteTests
{
    [Fact]
    public async Task DeleteClinic_WhenSucceeded_ReturnsNoContent()
    {
        var adminId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var clinicId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var clinicService = new Mock<IClinicService>();
        clinicService.Setup(service => service.DeleteAsync(
                It.Is<ClinicAccessContext>(access => access.IsAdmin),
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = ClinicsControllerTestHelper.CreateController(
            clinicService,
            ClinicsControllerTestHelper.CreateAdmin(adminId));

        var result = await controller.DeleteClinic(clinicId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }
}

public class ClinicsControllerDoctorManagementTests
{
    [Fact]
    public async Task GetClinicDoctors_WhenSucceeded_ReturnsOk()
    {
        var managerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var clinicId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var doctors = new List<ClinicDoctorMemberDto>
        {
            new(managerId, "Manager", "+15551111111", Domain.Enums.UserRole.ClinicManager, "General", true),
        };

        var clinicService = new Mock<IClinicService>();
        clinicService.Setup(service => service.GetDoctorsAsync(
                It.IsAny<ClinicAccessContext>(),
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>.Success(doctors));

        var controller = ClinicsControllerTestHelper.CreateController(
            clinicService,
            ClinicsControllerTestHelper.CreateClinicManager(managerId));

        var result = await controller.GetClinicDoctors(clinicId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(doctors);
    }

    [Fact]
    public async Task GetDoctorCandidates_ReturnsEveryDoctorInTheSystem()
    {
        var managerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var otherDoctorId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var doctors = new List<Phisio.Application.Doctors.DoctorDto>
        {
            new(managerId, "Manager", "+15551111111", "General", "MD-1", "Addr", DateTime.UtcNow, IsClinicManager: true),
            new(otherDoctorId, "Sara", "+15552222222", "Ortho", "MD-2", "Addr", DateTime.UtcNow),
        };

        var doctorService = new Mock<IAdminDoctorService>();
        doctorService.Setup(service => service.GetAllAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<Phisio.Application.Doctors.DoctorDto>>.Success(doctors));

        var controller = ClinicsControllerTestHelper.CreateController(
            new Mock<IClinicService>(),
            ClinicsControllerTestHelper.CreateClinicManager(managerId),
            doctorService);

        var result = await controller.GetDoctorCandidates(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new List<ClinicDoctorCandidateDto>
        {
            new(managerId, "Manager", "+15551111111", "General", true),
            new(otherDoctorId, "Sara", "+15552222222", "Ortho", false),
        });
    }

    [Fact]
    public async Task AddClinicDoctor_WhenDuplicate_ReturnsBadRequest()
    {
        var managerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var clinicId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var doctorId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        var clinicService = new Mock<IClinicService>();
        clinicService.Setup(service => service.AddDoctorAsync(
                It.IsAny<ClinicAccessContext>(),
                clinicId,
                doctorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicDoctorMemberDto>.Failure([ClinicErrors.DoctorAlreadyAssigned]));

        var controller = ClinicsControllerTestHelper.CreateController(
            clinicService,
            ClinicsControllerTestHelper.CreateClinicManager(managerId));

        var result = await controller.AddClinicDoctor(clinicId, doctorId, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task RemoveClinicDoctor_WhenRemovingClinicManager_ReturnsBadRequest()
    {
        var managerId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var clinicId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var clinicService = new Mock<IClinicService>();
        clinicService.Setup(service => service.RemoveDoctorAsync(
                It.IsAny<ClinicAccessContext>(),
                clinicId,
                managerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure([ClinicErrors.CannotRemoveClinicManager]));

        var controller = ClinicsControllerTestHelper.CreateController(
            clinicService,
            ClinicsControllerTestHelper.CreateClinicManager(managerId));

        var result = await controller.RemoveClinicDoctor(clinicId, managerId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
