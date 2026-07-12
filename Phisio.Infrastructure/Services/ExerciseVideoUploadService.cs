using Microsoft.Extensions.Hosting;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;

namespace Phisio.Infrastructure.Services;

public class ExerciseVideoUploadService : IExerciseVideoUploadService
{
    private const string UploadsFolderName = "uploads";
    private const string ExercisesFolderName = "exercises";
    private const string PublicUploadsPath = "/uploads";
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
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Video file is required."]);
        }

        if (fileLength <= 0)
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Video file is required."]);
        }

        if (fileLength > ExerciseUploadLimits.MaxFileSizeBytes)
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Video file must not exceed 50 MB."]);
        }

        if (!IsMp4File(contentType, originalFileName))
        {
            return AuthResult<UploadExerciseVideoResponse>.Failure(["Only MP4 video files are allowed."]);
        }

        var fileName = ExerciseVideoFileNameGenerator.ResolveUniqueFileName(_exercisesDirectory, exerciseName);
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

    private static bool IsMp4File(string contentType, string originalFileName)
    {
        if (!originalFileName.EndsWith(ExerciseUploadLimits.Mp4Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(contentType)
            || string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(contentType, ExerciseUploadLimits.Mp4ContentType, StringComparison.OrdinalIgnoreCase);
    }
}
