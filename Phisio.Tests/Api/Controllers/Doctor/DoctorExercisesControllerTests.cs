using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Doctor;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;

namespace Phisio.Tests.Api.Controllers.Doctor;

internal static class DoctorExercisesControllerTestHelper
{
    public static DoctorExercisesController CreateController(
        Mock<IDoctorExerciseService> exerciseService,
        ClaimsPrincipal? user = null)
    {
        return new DoctorExercisesController(exerciseService.Object)
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
}

public class DoctorExercisesControllerGetExercisesTests
{
    [Fact]
    public async Task GetExercises_WhenExercisesExist_ReturnsOk()
    {
        // Arrange
        var exercises = new List<DoctorExerciseDto>
        {
            new(
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Hamstring Stretch",
                "Stretch the hamstring muscles.",
                "https://example.com/videos/hamstring-stretch.mp4")
        };

        var exerciseService = new Mock<IDoctorExerciseService>();
        exerciseService.Setup(service => service.GetExercisesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorExerciseDto>>.Success(exercises));

        var controller = DoctorExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.GetExercises(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(exercises);
    }
}
