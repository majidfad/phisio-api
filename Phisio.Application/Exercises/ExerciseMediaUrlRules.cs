using Phisio.Domain.Enums;

namespace Phisio.Application.Exercises;

/// <summary>Shared media URL checks for exercise create/update validators.</summary>
public static class ExerciseMediaUrlRules
{
    public static bool IsValid(ExerciseMediaType mediaType, string? videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            return mediaType == ExerciseMediaType.UploadedVideo;
        }

        var value = videoUrl.Trim();

        return mediaType switch
        {
            ExerciseMediaType.UploadedVideo => IsUploadedPath(value),
            ExerciseMediaType.Youtube =>
                Uri.TryCreate(value, UriKind.Absolute, out var yt)
                && yt.Scheme is "http" or "https"
                && (yt.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                    || yt.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)),
            ExerciseMediaType.ExternalVideo =>
                IsHttpsUrlWithExtension(value, [".mp4", ".webm", ".mov"]),
            ExerciseMediaType.Gif =>
                IsHttpsUrlWithExtension(value, [".gif", ".webp"])
                || (IsUploadedPath(value) && HasExtension(value, [".gif", ".webp"])),
            ExerciseMediaType.Animation =>
                IsHttpsUrlWithExtension(value, [".gif", ".webp", ".mp4", ".webm"])
                || (IsUploadedPath(value) && HasExtension(value, [".gif", ".webp", ".mp4", ".webm"])),
            _ => false,
        };
    }

    /// <summary>
    /// Accepts relative upload paths and absolute http(s) URLs whose path is under /uploads/
    /// (upload endpoints return absolute public URLs).
    /// </summary>
    private static bool IsUploadedPath(string value)
    {
        if (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpsUrlWithExtension(string value, string[] extensions)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return HasExtension(uri.AbsolutePath, extensions);
    }

    private static bool HasExtension(string value, string[] extensions)
    {
        var path = value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        return extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
