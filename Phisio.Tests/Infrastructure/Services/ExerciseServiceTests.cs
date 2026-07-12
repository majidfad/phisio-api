using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class ExerciseServiceGetAllTests
{
    [Fact]
    public async Task GetAllAsync_WhenNoExercisesExist_ReturnsEmptyList()
    {
        // Arrange
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenMultipleExercisesExist_ReturnsOrderedByCreatedAtDescending()
    {
        // Arrange
        var older = ExerciseBuilder.Create(title: "Older Exercise", createdAt: DateTime.UtcNow.AddDays(-3));
        var newer = ExerciseBuilder.Create(title: "Newer Exercise", createdAt: DateTime.UtcNow.AddDays(-1));
        var newest = ExerciseBuilder.Create(title: "Newest Exercise", createdAt: DateTime.UtcNow);

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [older, newer, newest]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(3);
        result.Value.Select(dto => dto.Title).Should()
            .ContainInOrder("Newest Exercise", "Newer Exercise", "Older Exercise");
    }

    [Fact]
    public async Task GetAllAsync_WhenDisabledExercisesExist_ReturnsOnlyActiveExercises()
    {
        // Arrange
        var active = ExerciseBuilder.Create(title: "Active Exercise");
        var disabled = ExerciseBuilder.Create(title: "Disabled Exercise");
        disabled.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [active, disabled]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().ContainSingle();
        result.Value[0].Title.Should().Be("Active Exercise");
    }

    [Fact]
    public async Task GetAllAsync_WhenInactiveOnlyRequested_ReturnsOnlyDisabledExercises()
    {
        // Arrange
        var active = ExerciseBuilder.Create(title: "Active Exercise");
        var disabled = ExerciseBuilder.Create(title: "Disabled Exercise", id: Guid.NewGuid());
        disabled.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [active, disabled]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetAllAsync(isEnabled: false);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].Title.Should().Be("Disabled Exercise");
        result.Value[0].IsEnabled.Should().BeFalse();
    }
}

public class ExerciseServiceGetByIdTests
{
    [Fact]
    public async Task GetByIdAsync_WhenExerciseExists_ReturnsExerciseDto()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create(title: "Hamstring Stretch");
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetByIdAsync(exercise.ExerciseId);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ExerciseId.Should().Be(exercise.ExerciseId);
        result.Value.Title.Should().Be(exercise.Title);
        result.Value.Description.Should().Be(exercise.Description);
        result.Value.VideoUrl.Should().Be(exercise.VideoUrl);
        result.Value.CreatedAt.Should().Be(exercise.CreatedAt);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExerciseNotFound_ReturnsFailure()
    {
        // Arrange
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Exercise not found.");
    }
}

public class ExerciseServiceCreateTests
{
    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_ReturnsCreatedExerciseDto()
    {
        // Arrange
        var request = ExerciseTestDataBuilder.CreateDto();
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be(request.Title);
        result.Value.Description.Should().Be(request.Description);
        result.Value.VideoUrl.Should().Be(request.VideoUrl);

        dbContext.Object.Exercises.Should().ContainSingle()
            .Which.Title.Should().Be(request.Title);

        dbContext.Verify(
            context => context.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveFails_ReturnsValidationFailure()
    {
        // Arrange
        var request = ExerciseTestDataBuilder.CreateDto();
        var dbContext = AppDbContextMockFactory.CreateMock();

        dbContext.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Validation failed.", new Exception()));

        var sut = new ExerciseService(dbContext.Object);

        // Act
        var act = () => sut.CreateAsync(request);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();

        dbContext.Verify(
            context => context.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class ExerciseServiceUpdateTests
{
    [Fact]
    public async Task UpdateAsync_WhenExerciseExists_ReturnsUpdatedExerciseDto()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create();
        var request = ExerciseTestDataBuilder.UpdateRequest();
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.UpdateAsync(exercise.ExerciseId, request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ExerciseId.Should().Be(exercise.ExerciseId);
        result.Value.Title.Should().Be(request.Title);
        result.Value.Description.Should().Be(request.Description);
        result.Value.VideoUrl.Should().Be(request.VideoUrl);

        dbContext.Verify(
            context => context.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenExerciseNotFound_ReturnsFailure()
    {
        // Arrange
        var request = ExerciseTestDataBuilder.UpdateRequest();
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Exercise not found.");

        dbContext.Verify(
            context => context.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenSaveFails_ReturnsValidationFailure()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create();
        var request = ExerciseTestDataBuilder.UpdateRequest();
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);

        dbContext.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Validation failed.", new Exception()));

        var sut = new ExerciseService(dbContext.Object);

        // Act
        var act = () => sut.UpdateAsync(exercise.ExerciseId, request);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}

public class ExerciseServiceDeleteTests
{
    [Fact]
    public async Task DeleteAsync_WhenExerciseExists_SoftDeletesExerciseAndAssignments()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create();
        var assignment = ExerciseAssignmentBuilder.ForExercise(exercise.ExerciseId);
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise], userExercises: [assignment]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.DeleteAsync(exercise.ExerciseId);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeTrue();
        dbContext.Object.Exercises.Should().BeEmpty();

        var disabledExercise = await dbContext.Object.Exercises
            .IgnoreQueryFilters()
            .SingleAsync();
        disabledExercise.IsEnabled.Should().BeFalse();

        var disabledAssignment = await dbContext.Object.UserExercises
            .IgnoreQueryFilters()
            .SingleAsync();
        disabledAssignment.IsEnabled.Should().BeFalse();

        dbContext.Verify(
            context => context.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenExerciseNotFound_ReturnsFailure()
    {
        // Arrange
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Exercise not found.");

        dbContext.Verify(
            context => context.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenExerciseAlreadyDisabled_ReturnsNotFound()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create();
        exercise.IsEnabled = false;
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.DeleteAsync(exercise.ExerciseId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Exercise not found.");
    }
}

public class ExerciseServiceActivateTests
{
    [Fact]
    public async Task ActivateAsync_WhenExerciseIsDisabled_RestoresExercise()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create();
        exercise.IsEnabled = false;
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.ActivateAsync(exercise.ExerciseId);

        // Assert
        result.Succeeded.Should().BeTrue();
        dbContext.Object.Exercises.IgnoreQueryFilters().Single().IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAsync_WhenExerciseAlreadyActive_ReturnsFailure()
    {
        // Arrange
        var exercise = ExerciseBuilder.Create();
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new ExerciseService(dbContext.Object);

        // Act
        var result = await sut.ActivateAsync(exercise.ExerciseId);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Exercise is already active.");
    }
}
