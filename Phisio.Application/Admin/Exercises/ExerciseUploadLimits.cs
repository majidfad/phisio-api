namespace Phisio.Application.Admin.Exercises;

public static class ExerciseUploadLimits
{
    /// <summary>Absolute request ceiling (500 MB). Runtime limit comes from <see cref="ExerciseUploadOptions"/>.</summary>
    public const long MaxFileSizeBytes = 524_288_000;

    public const string Mp4ContentType = "video/mp4";

    public const string GifContentType = "image/gif";

    public const string Mp4Extension = ".mp4";

    public const string GifExtension = ".gif";
}
