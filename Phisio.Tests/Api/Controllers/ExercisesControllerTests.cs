using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Common;
using Phisio.Application.Exercises;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Api.Controllers;

public class ExercisesControllerGetExercisesTests
{
    [Fact]
    public async Task GetExercises_WhenExercisesExist_ReturnsOk()
    {
        // Arrange
        var exercises = new List<ExerciseDto>
        {
            new(
                Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Hamstring Stretch",
                "Stretch the hamstring muscles.",
                "",
                "https://example.com/videos/hamstring-stretch",
                ExerciseMediaType.UploadedVideo,
                ExerciseBodyRegion.Other,
                ExerciseEquipment.None,
                ExerciseDifficulty.Moderate,
                CreatedByDoctorId: null,
                IsClinicShared: true,
                CreatedAt: new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc))
        };

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<ExerciseDto>>.Success(exercises));

        var controller = ExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.GetExercises(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(exercises);
    }
}

public class ExercisesControllerGetExerciseTests
{
    [Fact]
    public async Task GetExercise_WhenExerciseExists_ReturnsOk()
    {
        // Arrange
        var exerciseId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var exercise = new ExerciseDto(
            exerciseId,
            "Hamstring Stretch",
            "Stretch the hamstring muscles.",
            "",
            "https://example.com/videos/hamstring-stretch",
            ExerciseMediaType.UploadedVideo,
            ExerciseBodyRegion.Other,
            ExerciseEquipment.None,
            ExerciseDifficulty.Moderate,
            CreatedByDoctorId: null,
            IsClinicShared: true,
            CreatedAt: new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.GetByIdAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Success(exercise));

        var controller = ExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.GetExercise(exerciseId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(exercise);
    }

    [Fact]
    public async Task GetExercise_WhenExerciseNotFound_ReturnsNotFound()
    {
        // Arrange
        var exerciseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.GetByIdAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Failure(["Exercise not found."]));

        var controller = ExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.GetExercise(exerciseId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
