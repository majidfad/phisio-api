using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Doctor;
using Phisio.Application.Common;
using Phisio.Application.DoctorDashboard;

namespace Phisio.Tests.Api.Controllers.Doctor;

internal static class DoctorDashboardControllerTestHelper
{
    public static DoctorDashboardController CreateController(
        Mock<IDoctorDashboardService> dashboardService,
        ClaimsPrincipal? user = null)
    {
        return new DoctorDashboardController(dashboardService.Object)
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

public class DoctorDashboardControllerGetDashboardTests
{
    [Fact]
    public async Task GetDashboard_WhenDoctorIsAuthenticated_ReturnsOk()
    {
        // Arrange
        var doctorId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var dashboard = new DoctorDashboardDto(
            2,
            [
                new DoctorDashboardRecentPatientDto(
                    Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
                    "Alice Patient",
                    "+15551111111")
            ]);

        var dashboardService = new Mock<IDoctorDashboardService>();
        dashboardService.Setup(service => service.GetDashboardAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<DoctorDashboardDto>.Success(dashboard));

        var controller = DoctorDashboardControllerTestHelper.CreateController(
            dashboardService,
            DoctorDashboardControllerTestHelper.CreateAuthenticatedDoctor(doctorId));

        // Act
        var result = await controller.GetDashboard(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(dashboard);
    }
}
