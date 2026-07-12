using FluentAssertions;
using Phisio.Application.Admin.Exercises;

namespace Phisio.Tests.Application.Admin.Exercises;

public class ExerciseVideoFileNameGeneratorTests
{
    [Fact]
    public void SanitizeBaseName_WhenNameHasSpaces_ReplacesWithHyphens()
    {
        ExerciseVideoFileNameGenerator.SanitizeBaseName("Hamstring Stretch")
            .Should().Be("Hamstring-Stretch");
    }

    [Fact]
    public void ResolveUniqueFileName_WhenFileDoesNotExist_ReturnsBaseNameWithExtension()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        try
        {
            // Act
            var fileName = ExerciseVideoFileNameGenerator.ResolveUniqueFileName(directory, "Neck Roll");

            // Assert
            fileName.Should().Be("Neck-Roll.mp4");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResolveUniqueFileName_WhenFileExists_AppendsUniqueSuffix()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Neck-Roll.mp4"), "existing");

        try
        {
            // Act
            var fileName = ExerciseVideoFileNameGenerator.ResolveUniqueFileName(directory, "Neck Roll");

            // Assert
            fileName.Should().StartWith("Neck-Roll-");
            fileName.Should().EndWith(".mp4");
            fileName.Should().NotBe("Neck-Roll.mp4");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
