using Phisio.Application.Admin.Exercises;
using Phisio.Application.Exercises;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Tests.TestDataBuilder;

internal static class ExerciseTestDataBuilder
{
    public static CreateExerciseDto CreateDto(
        string title = "Neck Stretch",
        string description = "Gentle neck mobility exercise.",
        string? videoUrl = "/uploads/exercises/neck-stretch.mp4") =>
        new()
        {
            Title = title,
            Description = description,
            Instructions = string.Empty,
            VideoUrl = videoUrl,
            MediaType = ExerciseMediaType.UploadedVideo,
            BodyRegion = ExerciseBodyRegion.Other,
            Equipment = ExerciseEquipment.None,
            Difficulty = ExerciseDifficulty.Moderate,
        };

    public static UpdateExerciseRequest UpdateRequest(
        string title = "Updated Neck Stretch",
        string description = "Updated description.",
        string? videoUrl = "/uploads/exercises/updated.mp4") =>
        new()
        {
            Title = title,
            Description = description,
            Instructions = string.Empty,
            VideoUrl = videoUrl,
            MediaType = ExerciseMediaType.UploadedVideo,
            BodyRegion = ExerciseBodyRegion.Other,
            Equipment = ExerciseEquipment.None,
            Difficulty = ExerciseDifficulty.Moderate,
        };
}

internal static class ExerciseBuilder
{
    public static Exercise Create(
        string title = "Neck Stretch",
        string description = "Gentle neck mobility exercise.",
        string? videoUrl = "/uploads/exercises/neck-stretch.mp4",
        Guid? id = null,
        DateTime? createdAt = null,
        Guid? createdByDoctorId = null) =>
        new()
        {
            ExerciseId = id ?? Guid.NewGuid(),
            Title = title,
            Description = description,
            VideoUrl = videoUrl,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            CreatedByDoctorId = createdByDoctorId,
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
