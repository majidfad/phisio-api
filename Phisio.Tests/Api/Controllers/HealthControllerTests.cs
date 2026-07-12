using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Controllers;

namespace Phisio.Tests.Api.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOkWithHealthyStatus()
    {
        var controller = new HealthController();

        var result = controller.Get();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { status = "healthy" });
    }
}
