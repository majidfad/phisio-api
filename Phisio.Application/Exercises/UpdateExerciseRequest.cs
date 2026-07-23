using Phisio.Domain.Enums;

namespace Phisio.Application.Exercises;

public sealed class UpdateExerciseRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }

    public ExerciseMediaType MediaType { get; set; } = ExerciseMediaType.UploadedVideo;

    public ExerciseBodyRegion BodyRegion { get; set; } = ExerciseBodyRegion.Other;

    public ExerciseEquipment Equipment { get; set; } = ExerciseEquipment.None;

    public ExerciseDifficulty Difficulty { get; set; } = ExerciseDifficulty.Moderate;
}
