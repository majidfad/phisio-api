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

    public static string ResolveUniqueFileName(string directory, string exerciseName, string extension = ExerciseUploadLimits.Mp4Extension)
    {
        var baseName = SanitizeBaseName(exerciseName);

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "exercise";
        }

        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        var candidate = baseName + extension;

        if (!File.Exists(Path.Combine(directory, candidate)))
        {
            return candidate;
        }

        candidate = $"{baseName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";

        if (!File.Exists(Path.Combine(directory, candidate)))
        {
            return candidate;
        }

        return $"{baseName}-{Guid.NewGuid():N}{extension}";
    }

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex InvalidCharsRegex();
}
