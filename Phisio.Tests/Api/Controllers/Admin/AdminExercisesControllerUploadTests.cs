using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.Exercises;

namespace Phisio.Tests.Api.Controllers.Admin;

public class AdminExercisesControllerUploadExerciseVideoTests
{
    [Fact]
    public async Task UploadExerciseVideo_WhenUploadSucceeds_ReturnsOk()
    {
        // Arrange
        var response = new UploadExerciseVideoResponse
        {
            VideoUrl = "http://localhost/uploads/exercises/Hamstring-Stretch.mp4",
            FileName = "Hamstring-Stretch.mp4",
        };

        var exerciseService = new Mock<IExerciseService>();
        var uploadService = new Mock<IExerciseVideoUploadService>();
        uploadService.Setup(service => service.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<UploadExerciseVideoResponse>.Success(response));

        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService, uploadService);
        controller.ControllerContext.HttpContext.Request.Scheme = "http";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost", 5111);

        await using var stream = new MemoryStream([0x00, 0x01]);
        var file = new FormFile(stream, 0, stream.Length, "file", "stretch.mp4")
        {
            Headers = new HeaderDictionary(),
            ContentType = ExerciseUploadLimits.Mp4ContentType,
        };

        // Act
        var result = await controller.UploadExerciseVideo(file, "Hamstring Stretch", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task UploadExerciseVideo_WhenFileMissing_ReturnsBadRequest()
    {
        // Arrange
        var exerciseService = new Mock<IExerciseService>();
        var uploadService = new Mock<IExerciseVideoUploadService>();
        var controller = AdminExercisesControllerTestHelper.CreateController(exerciseService, uploadService);

        // Act
        var result = await controller.UploadExerciseVideo(null!, "Hamstring Stretch", CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
