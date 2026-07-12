using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.Exercises;

namespace Phisio.Tests.Api.Controllers.Admin;

public class AdminExercisesControllerGetExercisesTests
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
                "https://example.com/videos/hamstring-stretch",
                new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc))
        };

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.GetAllAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<IReadOnlyList<ExerciseDto>>.Success(exercises));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.GetExercises(cancellationToken: CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(exercises);
    }
}

public class AdminExercisesControllerGetExerciseTests
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
            "https://example.com/videos/hamstring-stretch",
            new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.GetByIdAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Success(exercise));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

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

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.GetExercise(exerciseId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AdminExercisesControllerCreateExerciseTests
{
    [Fact]
    public async Task CreateExercise_WhenCreationSucceeds_ReturnsCreated()
    {
        // Arrange
        var request = new CreateExerciseDto
        {
            Title = "Hamstring Stretch",
            Description = "Stretch the hamstring muscles.",
            VideoUrl = "https://example.com/videos/hamstring-stretch",
        };

        var createdExercise = new ExerciseDto(
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            request.Title,
            request.Description,
            request.VideoUrl,
            new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Success(createdExercise));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.CreateExercise(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(createdExercise);
    }

    [Fact]
    public async Task CreateExercise_WhenCreationFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateExerciseDto
        {
            Title = "Hamstring Stretch",
            Description = "Stretch the hamstring muscles.",
            VideoUrl = "https://example.com/videos/hamstring-stretch",
        };

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Failure(["An exercise with this title already exists."]));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.CreateExercise(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}

public class AdminExercisesControllerUpdateExerciseTests
{
    [Fact]
    public async Task UpdateExercise_WhenUpdateSucceeds_ReturnsOk()
    {
        // Arrange
        var exerciseId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var request = new UpdateExerciseRequest
        {
            Title = "Updated Hamstring Stretch",
            Description = "Updated stretch instructions.",
            VideoUrl = "https://example.com/videos/updated-hamstring-stretch",
        };

        var updatedExercise = new ExerciseDto(
            exerciseId,
            request.Title,
            request.Description,
            request.VideoUrl,
            new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.UpdateAsync(exerciseId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Success(updatedExercise));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.UpdateExercise(exerciseId, request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(updatedExercise);
    }

    [Fact]
    public async Task UpdateExercise_WhenExerciseNotFound_ReturnsNotFound()
    {
        // Arrange
        var exerciseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var request = new UpdateExerciseRequest
        {
            Title = "Updated Hamstring Stretch",
            Description = "Updated stretch instructions.",
            VideoUrl = "https://example.com/videos/updated-hamstring-stretch",
        };

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.UpdateAsync(exerciseId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ExerciseDto>.Failure(["Exercise not found."]));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.UpdateExercise(exerciseId, request, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class AdminExercisesControllerDeleteExerciseTests
{
    [Fact]
    public async Task DeleteExercise_WhenDeletionSucceeds_ReturnsNoContent()
    {
        // Arrange
        var exerciseId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.DeleteAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Success(true));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.DeleteExercise(exerciseId, CancellationToken.None);

        // Assert
        var noContentResult = result.Should().BeOfType<NoContentResult>().Subject;
        noContentResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task DeleteExercise_WhenExerciseNotFound_ReturnsNotFound()
    {
        // Arrange
        var exerciseId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        var exerciseService = new Mock<IExerciseService>();
        exerciseService.Setup(service => service.DeleteAsync(exerciseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<bool>.Failure(["Exercise not found."]));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService);

        // Act
        var result = await controller.DeleteExercise(exerciseId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
