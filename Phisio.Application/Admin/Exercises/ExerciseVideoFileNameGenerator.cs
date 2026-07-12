using System.Text.RegularExpressions;

namespace Phisio.Application.Admin.Exercises;

public static partial class ExerciseVideoFileNameGenerator
{
    private static readonly Regex InvalidFileNameCharacters = InvalidCharsRegex();

    public static string SanitizeBaseName(string exerciseName)
    {
        if (string.IsNullOrWhiteSpace(exerciseName))
        {
            return string.Empty;
        }

        var sanitized = InvalidFileNameCharacters.Replace(exerciseName.Trim(), string.Empty)
            .Replace(' ', '-');

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        return sanitized.Trim('-');
    }

    public static string ResolveUniqueFileName(string directory, string exerciseName)
    {
        var baseName = SanitizeBaseName(exerciseName);

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "exercise";
        }

        var candidate = baseName + ExerciseUploadLimits.Mp4Extension;

        if (!File.Exists(Path.Combine(directory, candidate)))
        {
            return candidate;
        }

        candidate = $"{baseName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ExerciseUploadLimits.Mp4Extension}";

        if (!File.Exists(Path.Combine(directory, candidate)))
        {
            return candidate;
        }

        return $"{baseName}-{Guid.NewGuid():N}{ExerciseUploadLimits.Mp4Extension}";
    }

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex InvalidCharsRegex();
}
