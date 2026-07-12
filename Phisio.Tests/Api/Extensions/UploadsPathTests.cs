using FluentAssertions;
using Phisio.Api.Extensions;

namespace Phisio.Tests.Api.Extensions;

public class UploadsPathTests
{
    [Fact]
    public void ResolvePhysicalPath_WhenContentRootIsValid_ReturnsUploadsUnderContentRoot()
    {
        // Arrange
        var contentRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        // Act
        var uploadsPath = UploadsPath.ResolvePhysicalPath(contentRoot);

        // Assert
        uploadsPath.Should().Be(Path.Combine(contentRoot, UploadsPath.UploadsFolderName));
    }

    [Theory]
    [InlineData("/uploads/../appsettings.json")]
    [InlineData("/uploads/exercises/../../secret.txt")]
    [InlineData("/uploads//exercises/sample.mp4")]
    public void ContainsPathTraversal_WhenRequestIsUnsafe_ReturnsTrue(string requestPath)
    {
        UploadsPath.ContainsPathTraversal(requestPath).Should().BeTrue();
    }

    [Theory]
    [InlineData("/uploads/exercises/sample.mp4")]
    [InlineData("/uploads/images/thumbnail.png")]
    public void ContainsPathTraversal_WhenRequestIsSafe_ReturnsFalse(string requestPath)
    {
        UploadsPath.ContainsPathTraversal(requestPath).Should().BeFalse();
    }

    [Fact]
    public void BuildSampleUrl_WhenBaseUrlHasTrailingSlash_NormalizesUrl()
    {
        UploadsPath.BuildSampleUrl("http://localhost:5111/")
            .Should().Be("http://localhost:5111/uploads/exercises/sample.mp4");
    }
}
