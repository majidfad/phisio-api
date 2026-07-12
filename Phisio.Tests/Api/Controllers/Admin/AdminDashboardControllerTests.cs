using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Admin.Dashboard;
using Phisio.Application.Common;

namespace Phisio.Tests.Api.Controllers.Admin;

public class AdminDashboardControllerGetStatsTests
{
    [Fact]
    public async Task GetStats_WhenStatsExist_ReturnsOk()
    {
        // Arrange
        var stats = new AdminDashboardStatsDto(3, 12, 8);

        var dashboardService = new Mock<IAdminDashboardService>();
        dashboardService.Setup(service => service.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<AdminDashboardStatsDto>.Success(stats));

        var controller = new AdminDashboardController(dashboardService.Object);

        // Act
        var result = await controller.GetStats(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(stats);
    }
}
