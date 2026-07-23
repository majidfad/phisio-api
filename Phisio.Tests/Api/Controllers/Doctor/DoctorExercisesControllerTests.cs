using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Api.Controllers.Doctor;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Api.Controllers.Doctor;

internal static class DoctorExercisesControllerTestHelper
{
    public static DoctorExercisesController CreateController(
        Mock<IDoctorExerciseService> exerciseService,
        Mock<IExerciseVideoUploadService> exerciseVideoUploadService,
        ClaimsPrincipal? user = null)
    {
        return new DoctorExercisesController(exerciseService.Object, exerciseVideoUploadService.Object)
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

public class DoctorExercisesControllerGetLibraryTests
{
    [Fact]
    public async Task GetLibrary_WhenExercisesExist_ReturnsOk()
    {
        var doctorId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var exercises = new List<DoctorExerciseDto>
        {
            new(
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Hamstring Stretch",
                "Stretch the hamstring muscles.",
                "",
                "https://example.com/videos/hamstring-stretch.mp4",
                ExerciseMediaType.UploadedVideo,
                ExerciseBodyRegion.Other,
                ExerciseEquipment.None,
                ExerciseDifficulty.Moderate,
                CreatedByDoctorId: doctorId,
                IsOwnedByCurrentDoctor: true,
                CreatedAt: DateTime.UtcNow)
        };

        var exerciseService = new Mock<IDoctorExerciseService>();
        var exerciseVideoUploadService = new Mock<IExerciseVideoUploadService>();
        exerciseService.Setup(service => service.GetLibraryAsync(doctorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<DoctorExerciseDto>>.Success(exercises));

        var controller = DoctorExercisesControllerTestHelper.CreateController(
            exerciseService,
            exerciseVideoUploadService,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, doctorId.ToString())],
                authenticationType: "Test")));

        var result = await controller.GetLibrary(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(exercises);
    }
}
