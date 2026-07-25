namespace Phisio.Application.Admin.Exercises;

/// <summary>
/// Exercise media upload limits from the "ExerciseUpload" configuration section.
///
/// Environment variable:
///   ExerciseUpload__MaxFileSizeBytes=524288000
///
/// Docker Compose:
///   ExerciseUpload__MaxFileSizeBytes: ${EXERCISE_UPLOAD_MAX_BYTES:-524288000}
/// </summary>
public class ExerciseUploadOptions
{
    public const string SectionName = "ExerciseUpload";

    /// <summary>Default: 500 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = ExerciseUploadLimits.MaxFileSizeBytes;
}
