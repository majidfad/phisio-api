using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using Phisio.Application.Admin.Exercises;
using Phisio.Infrastructure.Services;

namespace Phisio.Tests.Infrastructure.Services;

public class ExerciseVideoUploadServiceTests
{
    [Fact]
    public async Task UploadAsync_WhenFileIsValid_SavesMp4AndReturnsVideoUrl()
    {
        // Arrange
        var contentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(contentRoot);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.Setup(env => env.ContentRootPath).Returns(contentRoot);

        var sut = new ExerciseVideoUploadService(hostEnvironment.Object);
        await using var stream = new MemoryStream([0x00, 0x01, 0x02]);

        try
        {
            // Act
            var result = await sut.UploadAsync(
                "Hamstring Stretch",
                stream,
                ExerciseUploadLimits.Mp4ContentType,
                "stretch.mp4",
                stream.Length,
                "http://localhost:5111");

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.VideoUrl.Should().Be("http://localhost:5111/uploads/exercises/Hamstring-Stretch.mp4");
            result.Value.FileName.Should().Be("Hamstring-Stretch.mp4");

            var savedPath = Path.Combine(contentRoot, "uploads", "exercises", result.Value.FileName);
            File.Exists(savedPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task UploadAsync_WhenFileIsNotMp4_ReturnsFailure()
    {
        // Arrange
        var contentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(contentRoot);

        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.Setup(env => env.ContentRootPath).Returns(contentRoot);

        var sut = new ExerciseVideoUploadService(hostEnvironment.Object);
        await using var stream = new MemoryStream([0x00]);

        try
        {
            // Act
            var result = await sut.UploadAsync(
                "Stretch",
                stream,
                "video/webm",
                "stretch.webm",
                stream.Length,
                "http://localhost:5111");

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Should().Be("Only MP4 video files are allowed.");
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}
