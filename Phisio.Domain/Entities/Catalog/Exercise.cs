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

    public ExerciseEquipment Equipment { get; set; } = ExerciseEquipment.None;

    public ExerciseDifficulty Difficulty { get; set; } = ExerciseDifficulty.Moderate;

    /// <summary>Null means admin catalog exercise.</summary>
    public Guid? CreatedByDoctorId { get; set; }

    public ICollection<UserExercise> UserExercises { get; set; } = new List<UserExercise>();

    public ICollection<ExerciseCategoryLink> CategoryLinks { get; set; } = new List<ExerciseCategoryLink>();
}
