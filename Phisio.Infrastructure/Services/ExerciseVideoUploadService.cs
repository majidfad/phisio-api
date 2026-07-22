using Microsoft.Extensions.Hosting;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;

namespace Phisio.Infrastructure.Services;

public class ExerciseVideoUploadService : IExerciseVideoUploadService
{
    private const string UploadsFolderName = "uploads";
    private const string ExercisesFolderName = "exercises";
    private const string PublicExercisesPath = "/uploads/exercises";

    private readonly string _exercisesDirectory;

    public ExerciseVideoUploadService(IHostEnvironment hostEnvironment)
    {
        _exercisesDirectory = Path.Combine(
            hostEnvironment.ContentRootPath,
            UploadsFolderName,
            ExercisesFolderName);

        Directory.CreateDirectory(_exercisesDirectory);
    }

    public async Task<AuthResult<UploadExerciseVideoResponse>> UploadAsync(
        string exerciseName,
        Stream fileStream,
        string contentType,
        string originalFileName,
        long fileLength,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exerciseName))
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Exercise name is required."]);
        }

        if (fileStream is null || !fileStream.CanRead)
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Media file is required."]);
        }

        if (fileLength <= 0)
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Media file is required."]);
        }

        if (fileLength > ExerciseUploadLimits.MaxFileSizeBytes)
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Media file must not exceed 50 MB."]);
        }

        var extension = ResolveAllowedExtension(contentType, originalFileName);
        if (extension is null)
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Only MP4 video or GIF files are allowed."]);
        }

        var fileName = ExerciseVideoFileNameGenerator.ResolveUniqueFileName(
            _exercisesDirectory,
            exerciseName,
            extension);
        var physicalPath = Path.Combine(_exercisesDirectory, fileName);

        if (!physicalPath.StartsWith(_exercisesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Invalid file path."]);
        }

        await using var output = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await fileStream.CopyToAsync(output, cancellationToken);

        var normalizedBaseUrl = publicBaseUrl.TrimEnd('/');
        var videoUrl = $"{normalizedBaseUrl}{PublicExercisesPath}/{fileName}";

        return AuthResult<UploadExerciseVideoResponse>.Success(new UploadExerciseVideoResponse
        {
            VideoUrl = videoUrl,
            FileName = fileName,
        });
    }

    private static string? ResolveAllowedExtension(string contentType, string originalFileName)
    {
        if (originalFileName.EndsWith(ExerciseUploadLimits.Mp4Extension, StringComparison.OrdinalIgnoreCase)
            && IsAllowedContentType(contentType, ExerciseUploadLimits.Mp4ContentType))
        {
            return ExerciseUploadLimits.Mp4Extension;
        }

        if (originalFileName.EndsWith(ExerciseUploadLimits.GifExtension, StringComparison.OrdinalIgnoreCase)
            && IsAllowedContentType(contentType, ExerciseUploadLimits.GifContentType))
        {
            return ExerciseUploadLimits.GifExtension;
        }

        return null;
    }

    private static bool IsAllowedContentType(string contentType, string expected)
    {
        if (string.IsNullOrWhiteSpace(contentType)
            || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(contentType, expected, StringComparison.OrdinalIgnoreCase);
    }
}
