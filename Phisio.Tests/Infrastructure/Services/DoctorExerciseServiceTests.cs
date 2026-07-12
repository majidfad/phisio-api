using FluentAssertions;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorExerciseServiceGetExercisesTests
{
    [Fact]
    public async Task GetExercisesAsync_WhenNoExercisesExist_ReturnsEmptyList()
    {
        // Arrange
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new DoctorExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExercisesAsync_WhenMultipleExercisesExist_ReturnsOrderedByCreatedAtDescending()
    {
        // Arrange
        var older = ExerciseBuilder.Create(title: "Older Exercise", createdAt: DateTime.UtcNow.AddDays(-3));
        var newer = ExerciseBuilder.Create(title: "Newer Exercise", createdAt: DateTime.UtcNow.AddDays(-1));
        var newest = ExerciseBuilder.Create(title: "Newest Exercise", createdAt: DateTime.UtcNow);

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [older, newer, newest]);
        var sut = new DoctorExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(3);
        result.Value.Select(dto => dto.Title).Should()
            .ContainInOrder("Newest Exercise", "Newer Exercise", "Older Exercise");
    }

    [Fact]
    public async Task GetExercisesAsync_WhenDisabledExercisesExist_ReturnsOnlyActiveExercises()
    {
        // Arrange
        var active = ExerciseBuilder.Create(title: "Active Exercise");
        var disabled = ExerciseBuilder.Create(title: "Disabled Exercise");
        disabled.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [active, disabled]);
        var sut = new DoctorExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().Title.Should().Be("Active Exercise");
    }

    [Fact]
    public async Task GetExercisesAsync_MapsExerciseFields()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create(
            title: "Squat",
            description: "Bodyweight squat",
            videoUrl: "https://example.com/squat.mp4");

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new DoctorExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        var dto = result.Value!.Single();
        dto.ExerciseId.Should().Be(exercise.ExerciseId);
        dto.Title.Should().Be("Squat");
        dto.Description.Should().Be("Bodyweight squat");
        dto.VideoUrl.Should().Be("https://example.com/squat.mp4");
    }

    [Fact]
    public async Task GetExercisesAsync_WhenDescriptionIsEmpty_ReturnsNullDescription()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create(title: "Plank", description: string.Empty);
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new DoctorExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetExercisesAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value!.Single().Description.Should().BeNull();
    }
}
