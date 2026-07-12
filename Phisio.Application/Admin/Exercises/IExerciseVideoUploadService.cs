using Phisio.Application.Common;

namespace Phisio.Application.Admin.Exercises;

public interface IExerciseVideoUploadService
{
    Task<AuthResult<UploadExerciseVideoResponse>> UploadAsync(
        string exerciseName,
        Stream fileStream,
        string contentType,
        string originalFileName,
        long fileLength,
        string publicBaseUrl,
        CancellationToken cancellationToken = default);
}
