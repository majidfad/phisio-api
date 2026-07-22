using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;
using Phisio.Api.Extensions;

namespace Phisio.Api.Controllers.Doctor;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.DoctorOnly)]
[Route("api/doctor/exercises")]
public class DoctorExercisesController : ControllerBase
{
    private readonly IDoctorExerciseService _exerciseService;
    private readonly IExerciseVideoUploadService _exerciseVideoUploadService;

    public DoctorExercisesController(
        IDoctorExerciseService exerciseService,
        IExerciseVideoUploadService exerciseVideoUploadService)
    {
        _exerciseService = exerciseService;
        _exerciseVideoUploadService = exerciseVideoUploadService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorExerciseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExercises(
        [FromQuery] string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var parsedScope = ParseScope(scope);
        var result = await _exerciseService.GetExercisesAsync(doctorId.Value, parsedScope, cancellationToken);
        return Ok(result.Value);
    }

    private static DoctorExerciseScope ParseScope(string? scope) =>
        scope?.Trim().ToLowerInvariant() switch
        {
            "mine" or "1" => DoctorExerciseScope.Mine,
            "clinic" or "2" => DoctorExerciseScope.Clinic,
            _ => DoctorExerciseScope.All,
        };

    [HttpPost]
    [ProducesResponseType(typeof(DoctorExerciseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateExercise(
        [FromBody] CreateDoctorExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _exerciseService.CreateAsync(doctorId.Value, request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DoctorExerciseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExercise(
        Guid id,
        [FromBody] UpdateDoctorExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _exerciseService.UpdateAsync(doctorId.Value, id, request, cancellationToken);
        if (!result.Succeeded)
        {
            var notFound = result.Errors.Contains("Exercise not found.");
            return notFound
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExercise(Guid id, CancellationToken cancellationToken)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _exerciseService.DeleteAsync(doctorId.Value, id, cancellationToken);
        if (!result.Succeeded)
        {
            var notFound = result.Errors.Contains("Exercise not found.");
            return notFound
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ExerciseUploadLimits.MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ExerciseUploadLimits.MaxFileSizeBytes)]
    [ProducesResponseType(typeof(UploadExerciseVideoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadExerciseMedia(
        IFormFile file,
        [FromForm] string exerciseName,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Media file is required." } });
        }

        var publicBaseUrl = $"{Request.Scheme}://{Request.Host}";
        await using var stream = file.OpenReadStream();

        var result = await _exerciseVideoUploadService.UploadAsync(
            exerciseName,
            stream,
            file.ContentType,
            file.FileName,
            file.Length,
            publicBaseUrl,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
