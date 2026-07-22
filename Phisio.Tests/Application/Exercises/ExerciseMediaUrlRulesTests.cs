using FluentAssertions;
using Phisio.Application.Exercises;
using Phisio.Domain.Enums;

namespace Phisio.Tests.Application.Exercises;

public class ExerciseMediaUrlRulesTests
{
    [Theory]
    [InlineData("/uploads/exercises/stretch.mp4")]
    [InlineData("uploads/exercises/stretch.mp4")]
    [InlineData("http://localhost:5111/uploads/exercises/stretch.mp4")]
    [InlineData("https://cdn.example.com/uploads/exercises/stretch.gif")]
    public void IsValid_UploadedVideo_AcceptsRelativeAndAbsoluteUploadUrls(string videoUrl)
    {
        ExerciseMediaUrlRules.IsValid(ExerciseMediaType.UploadedVideo, videoUrl).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/videos/stretch.mp4")]
    [InlineData("not-a-url")]
    [InlineData("/images/stretch.mp4")]
    public void IsValid_UploadedVideo_RejectsNonUploadUrls(string videoUrl)
    {
        ExerciseMediaUrlRules.IsValid(ExerciseMediaType.UploadedVideo, videoUrl).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://example.com/move.gif")]
    [InlineData("http://localhost:5111/uploads/exercises/move.gif")]
    [InlineData("/uploads/exercises/move.gif")]
    public void IsValid_Gif_AcceptsHttpsAndUploadedGifPaths(string videoUrl)
    {
        ExerciseMediaUrlRules.IsValid(ExerciseMediaType.Gif, videoUrl).Should().BeTrue();
    }

    [Fact]
    public void IsValid_Youtube_AcceptsYoutubeHosts()
    {
        ExerciseMediaUrlRules
            .IsValid(ExerciseMediaType.Youtube, "https://www.youtube.com/watch?v=abc123")
            .Should().BeTrue();
        ExerciseMediaUrlRules
            .IsValid(ExerciseMediaType.Youtube, "https://youtu.be/abc123")
            .Should().BeTrue();
    }
}
