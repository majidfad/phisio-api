namespace Phisio.Api.Extensions;

/// <summary>
/// Resolves and validates paths for the local uploads directory.
/// </summary>
public static class UploadsPath
{
    public const string UploadsFolderName = "uploads";

    public const string ExercisesFolderName = "exercises";

    public const string RequestPath = "/uploads";

    public const string SampleFileName = "sample.mp4";

    public static string ResolvePhysicalPath(string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var uploadsPath = Path.GetFullPath(Path.Combine(contentRootPath, UploadsFolderName));

        if (!uploadsPath.StartsWith(Path.GetFullPath(contentRootPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Uploads path must stay within the content root.");
        }

        return uploadsPath;
    }

    public static string ResolveExercisesPhysicalPath(string contentRootPath) =>
        Path.Combine(ResolvePhysicalPath(contentRootPath), ExercisesFolderName);

    public static bool ContainsPathTraversal(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        return requestPath.Contains('\\', StringComparison.Ordinal)
            || requestPath.Contains("//", StringComparison.Ordinal)
            || requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or "..");
    }

    public static string BuildSampleUrl(string baseUrl)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        return $"{normalizedBase}{RequestPath}/{ExercisesFolderName}/{SampleFileName}";
    }
}
