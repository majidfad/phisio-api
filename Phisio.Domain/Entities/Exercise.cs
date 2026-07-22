using Phisio.Domain.Common;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

public class Exercise : BaseEntity
{
    public Guid ExerciseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string? VideoUrl { get; set; }

    public ExerciseMediaType MediaType { get; set; } = ExerciseMediaType.UploadedVideo;

    public ExerciseBodyRegion BodyRegion { get; set; } = ExerciseBodyRegion.Other;

    public ExerciseEquipment Equipment { get; set; } = ExerciseEquipment.None;

    public ExerciseDifficulty Difficulty { get; set; } = ExerciseDifficulty.Moderate;

    /// <summary>Null means clinic/admin-owned shared exercise.</summary>
    public Guid? CreatedByDoctorId { get; set; }

    public bool IsClinicShared { get; set; } = true;

    public ICollection<UserExercise> UserExercises { get; set; } = new List<UserExercise>();
}
