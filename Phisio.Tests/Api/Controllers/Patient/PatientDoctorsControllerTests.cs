using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDoctors;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Api.Controllers.Patient;

public class PatientDoctorsControllerTests
{
    [Fact]
    public async Task GetDoctorClinics_WhenSucceeded_ReturnsOk()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var clinics = new List<PatientDoctorClinicOptionDto>
        {
            new(clinicId, "North Clinic", "Tehran", DoctorPatientStatus.Pending),
        };

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.GetDoctorClinicsAsync(patientId, doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<PatientDoctorClinicOptionDto>>.Success(clinics));

        var controller = CreateController(service, patientId);

        var result = await controller.GetDoctorClinics(doctorId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(clinics);
    }

    [Fact]
    public async Task GetDoctorClinics_WhenDoctorMissing_ReturnsNotFound()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.GetDoctorClinicsAsync(patientId, doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<PatientDoctorClinicOptionDto>>.Failure(
                [DoctorPatientErrors.DoctorNotFound]));

        var controller = CreateController(service, patientId);

        var result = await controller.GetDoctorClinics(doctorId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RequestLink_WhenSucceeded_PassesClinicIdAndReturnsOk()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var linked = new PatientLinkedDoctorDto(
            doctorId,
            "Dr Ahmadi",
            "Physio",
            "MD-1",
            "Address",
            "+15551111111",
            DoctorPatientStatus.Pending,
            DateTime.UtcNow,
            clinicId,
            "North Clinic");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.RequestLinkAsync(
                patientId,
                doctorId,
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientLinkedDoctorDto>.Success(linked));

        var controller = CreateController(service, patientId);

        var result = await controller.RequestLink(
            doctorId,
            new RequestPatientDoctorLinkDto(clinicId),
            CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(linked);
        service.Verify(
            s => s.RequestLinkAsync(patientId, doctorId, clinicId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestLink_WhenClinicNotFound_ReturnsNotFound()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.RequestLinkAsync(
                patientId,
                doctorId,
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientLinkedDoctorDto>.Failure([DoctorPatientErrors.ClinicNotFound]));

        var controller = CreateController(service, patientId);

        var result = await controller.RequestLink(
            doctorId,
            new RequestPatientDoctorLinkDto(clinicId),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RequestLink_WhenDoctorNotInClinic_ReturnsBadRequest()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.RequestLinkAsync(
                patientId,
                doctorId,
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientLinkedDoctorDto>.Failure(
                [DoctorPatientErrors.DoctorNotInClinic]));

        var controller = CreateController(service, patientId);

        var result = await controller.RequestLink(
            doctorId,
            new RequestPatientDoctorLinkDto(clinicId),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CancelRequest_WhenSucceeded_PassesClinicIdAndReturnsNoContent()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.CancelRequestAsync(
                patientId,
                doctorId,
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = CreateController(service, patientId);

        var result = await controller.CancelRequest(doctorId, clinicId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(
            s => s.CancelRequestAsync(patientId, doctorId, clinicId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Unlink_WhenSucceeded_PassesClinicIdAndReturnsNoContent()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.UnlinkAsync(
                patientId,
                doctorId,
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = CreateController(service, patientId);

        var result = await controller.Unlink(doctorId, clinicId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(
            s => s.UnlinkAsync(patientId, doctorId, clinicId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDoctorProfile_WhenClinicIdProvided_PassesClinicId()
    {
        var patientId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var clinicId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var profile = new PatientDoctorProfileDto(
            doctorId,
            "Dr Ahmadi",
            "Physio",
            "MD-1",
            "Address",
            "+15551111111",
            DoctorPatientStatus.Pending,
            DateTime.UtcNow,
            clinicId,
            "North Clinic");

        var service = new Mock<IPatientDoctorService>();
        service.Setup(s => s.GetDoctorProfileAsync(
                patientId,
                doctorId,
                clinicId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<PatientDoctorProfileDto>.Success(profile));

        var controller = CreateController(service, patientId);

        var result = await controller.GetDoctorProfile(doctorId, clinicId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(profile);
    }

    private static PatientDoctorsController CreateController(
        Mock<IPatientDoctorService> patientDoctorService,
        Guid? userId)
    {
        ClaimsPrincipal user = userId is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                authenticationType: "Test"));

        return new PatientDoctorsController(patientDoctorService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };
    }
}
