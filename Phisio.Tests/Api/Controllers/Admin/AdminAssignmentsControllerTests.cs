using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Admin.Assignments;
using Phisio.Application.Assignments;
using Phisio.Application.Common;

namespace Phisio.Tests.Api.Controllers.Admin;

public class AdminAssignmentsControllerGetReportTests
{
    [Fact]
    public async Task GetReport_WhenReportExists_ReturnsOk()
    {
        // Arrange
        var report = new List<AssignmentReportDto>
        {
            new("John Patient", "Dr. Jane Smith", ["Hamstring Stretch", "Squat"]),
        };

        var assignmentService = new Mock<IAssignmentService>();
        assignmentService.Setup(service => service.GetReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<AssignmentReportDto>>.Success(report));

        var controller = AdminAssignmentsControllerTestHelper.CreateController(assignmentService);

        // Act
        var result = await controller.GetReport(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(report);
    }
}
