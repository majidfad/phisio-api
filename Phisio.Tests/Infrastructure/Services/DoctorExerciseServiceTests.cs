using FluentAssertions;
using Phisio.Infrastructure.Services;
using Phisio.Tests.MockFactory;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Infrastructure.Services;

public class DoctorExerciseServiceGetLibraryTests
{
    [Fact]
    public async Task GetLibraryAsync_WhenNoExercisesExist_ReturnsEmptyList()
    {
        var doctorId = Guid.NewGuid();
        var dbContext = AppDbContextMockFactory.CreateMock();
        var sut = new DoctorExerciseService(dbContext.Object);

        var result = await sut.GetLibraryAsync(doctorId);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLibraryAsync_ReturnsOnlyCurrentDoctorExercisesOrderedByCreatedAtDescending()
    {
        var doctorId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();
        var older = ExerciseBuilder.Create(title: "Older Exercise", createdAt: DateTime.UtcNow.AddDays(-3));
        older.CreatedByDoctorId = doctorId;
        var newer = ExerciseBuilder.Create(title: "Newer Exercise", createdAt: DateTime.UtcNow.AddDays(-1));
        newer.CreatedByDoctorId = doctorId;
        var newest = ExerciseBuilder.Create(title: "Newest Exercise", createdAt: DateTime.UtcNow);
        newest.CreatedByDoctorId = doctorId;
        var otherDoctorExercise = ExerciseBuilder.Create(title: "Other Doctor");
        otherDoctorExercise.CreatedByDoctorId = otherDoctorId;
        var catalogExercise = ExerciseBuilder.Create(title: "Catalog");

        var dbContext = AppDbContextMockFactory.CreateMock(
            exercises: [older, newer, newest, otherDoctorExercise, catalogExercise]);
        var sut = new DoctorExerciseService(dbContext.Object);

        var result = await sut.GetLibraryAsync(doctorId);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(3);
        result.Value.Select(dto => dto.Title).Should()
            .ContainInOrder("Newest Exercise", "Newer Exercise", "Older Exercise");
    }

    [Fact]
    public async Task GetLibraryAsync_WhenDisabledExercisesExist_ReturnsOnlyActiveExercises()
    {
        var doctorId = Guid.NewGuid();
        var active = ExerciseBuilder.Create(title: "Active Exercise");
        active.CreatedByDoctorId = doctorId;
        var disabled = ExerciseBuilder.Create(title: "Disabled Exercise");
        disabled.CreatedByDoctorId = doctorId;
        disabled.IsEnabled = false;

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [active, disabled]);
        var sut = new DoctorExerciseService(dbContext.Object);

        var result = await sut.GetLibraryAsync(doctorId);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().Title.Should().Be("Active Exercise");
    }

    [Fact]
    public async Task GetLibraryAsync_MapsExerciseFields()
    {
        var doctorId = Guid.NewGuid();
        var exercise = ExerciseBuilder.Create(
            title: "Squat",
            description: "Bodyweight squat",
            videoUrl: "https://example.com/squat.mp4");
        exercise.CreatedByDoctorId = doctorId;

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new DoctorExerciseService(dbContext.Object);

        var result = await sut.GetLibraryAsync(doctorId);

        result.Succeeded.Should().BeTrue();
        var dto = result.Value!.Single();
        dto.ExerciseId.Should().Be(exercise.ExerciseId);
        dto.Title.Should().Be("Squat");
        dto.Description.Should().Be("Bodyweight squat");
        dto.VideoUrl.Should().Be("https://example.com/squat.mp4");
        dto.IsOwnedByCurrentDoctor.Should().BeTrue();
    }

    [Fact]
    public async Task GetLibraryAsync_WhenDescriptionIsEmpty_ReturnsNullDescription()
    {
        var doctorId = Guid.NewGuid();
        var exercise = ExerciseBuilder.Create(title: "Plank", description: string.Empty);
        exercise.CreatedByDoctorId = doctorId;
        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [exercise]);
        var sut = new DoctorExerciseService(dbContext.Object);

        var result = await sut.GetLibraryAsync(doctorId);

        result.Succeeded.Should().BeTrue();
        result.Value!.Single().Description.Should().BeNull();
    }

    [Fact]
    public async Task GetCatalogAsync_ReturnsOnlyAdminCatalogExercises()
    {
        var doctorId = Guid.NewGuid();
        var catalog = ExerciseBuilder.Create(title: "Catalog Exercise");
        var doctorOwned = ExerciseBuilder.Create(title: "Doctor Owned");
        doctorOwned.CreatedByDoctorId = doctorId;

        var dbContext = AppDbContextMockFactory.CreateMock(exercises: [catalog, doctorOwned]);
        var sut = new DoctorExerciseService(dbContext.Object);

        var result = await sut.GetCatalogAsync();

        result.Succeeded.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().Title.Should().Be("Catalog Exercise");
    }
}
