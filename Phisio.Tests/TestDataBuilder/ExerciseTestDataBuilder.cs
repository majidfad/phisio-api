using Phisio.Application.Admin.Exercises;
using Phisio.Application.Exercises;
using Phisio.Domain.Entities;

namespace Phisio.Tests.TestDataBuilder;

internal static class ExerciseTestDataBuilder
{
    public static CreateExerciseDto CreateDto(
        string title = "Neck Stretch",
        string description = "Gentle neck mobility exercise.",
        string? videoUrl = "https://example.com/neck-stretch") =>
        new()
        {
            Title = title,
            Description = description,
            VideoUrl = videoUrl,
        };

    public static UpdateExerciseRequest UpdateRequest(
        string title = "Updated Neck Stretch",
        string description = "Updated description.",
        string? videoUrl = "https://example.com/updated") =>
        new()
        {
            Title = title,
            Description = description,
            VideoUrl = videoUrl,
        };
}

internal static class ExerciseBuilder
{
    public static Exercise Create(
        string title = "Neck Stretch",
        string description = "Gentle neck mobility exercise.",
        string? videoUrl = "https://example.com/neck-stretch",
        Guid? id = null,
        DateTime? createdAt = null) =>
        new()
        {
            ExerciseId = id ?? Guid.NewGuid(),
            Title = title,
            Description = description,
            VideoUrl = videoUrl,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
}

internal static class ExerciseAssignmentBuilder
{
    public static UserExercise ForExercise(Guid exerciseId) =>
        new()
        {
            UserExerciseId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ExerciseId = exerciseId,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        };
}
