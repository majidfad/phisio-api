using Phisio.Domain.Enums;

namespace Phisio.Application.DoctorExercises;

public enum DoctorExerciseScope
{
    All = 0,
    Mine = 1,
    Clinic = 2,
}

public sealed record DoctorExerciseDto(
    Guid ExerciseId,
    string Title,
    string? Description,
    string Instructions,
    string? VideoUrl,
    ExerciseMediaType MediaType,
    ExerciseBodyRegion BodyRegion,
    ExerciseEquipment Equipment,
    ExerciseDifficulty Difficulty,
    Guid? CreatedByDoctorId,
    bool IsClinicShared,
    bool IsOwnedByCurrentDoctor,
    DateTime CreatedAt);

public sealed class CreateDoctorExerciseRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }

    public ExerciseMediaType MediaType { get; set; } = ExerciseMediaType.UploadedVideo;

    public ExerciseBodyRegion BodyRegion { get; set; } = ExerciseBodyRegion.Other;

    public ExerciseEquipment Equipment { get; set; } = ExerciseEquipment.None;

    public ExerciseDifficulty Difficulty { get; set; } = ExerciseDifficulty.Moderate;

    public bool IsClinicShared { get; set; }
}

public sealed class UpdateDoctorExerciseRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }

    public ExerciseMediaType MediaType { get; set; } = ExerciseMediaType.UploadedVideo;

    public ExerciseBodyRegion BodyRegion { get; set; } = ExerciseBodyRegion.Other;

    public ExerciseEquipment Equipment { get; set; } = ExerciseEquipment.None;

    public ExerciseDifficulty Difficulty { get; set; } = ExerciseDifficulty.Moderate;

    public bool IsClinicShared { get; set; }
}
