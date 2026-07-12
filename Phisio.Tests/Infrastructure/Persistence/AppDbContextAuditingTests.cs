using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Tests.Infrastructure.Persistence;

public class AppDbContextAuditingTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenExerciseAdded_SetsCreatedAt()
    {
        // Arrange
        await using var context = CreateContext();
        var beforeSave = DateTime.UtcNow;

        var exercise = new Exercise
        {
            ExerciseId = Guid.NewGuid(),
            Title = "Shoulder Stretch",
            Description = "Stretch the shoulder",
            CreatedAt = default,
        };

        context.Exercises.Add(exercise);

        // Act
        await context.SaveChangesAsync();

        // Assert
        exercise.CreatedAt.Should().BeOnOrAfter(beforeSave);
        exercise.CreatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserExerciseAdded_SetsCreatedAt()
    {
        // Arrange
        await using var context = CreateContext();
        var beforeSave = DateTime.UtcNow;

        var assignment = new UserExercise
        {
            UserExerciseId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ExerciseId = Guid.NewGuid(),
            AssignedAt = DateTime.UtcNow,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            CreatedAt = default,
        };

        context.UserExercises.Add(assignment);

        // Act
        await context.SaveChangesAsync();

        // Assert
        assignment.CreatedAt.Should().BeOnOrAfter(beforeSave);
        assignment.CreatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
