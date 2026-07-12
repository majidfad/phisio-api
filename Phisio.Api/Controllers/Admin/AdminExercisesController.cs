using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.Exercises;

namespace Phisio.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin/exercises")]
public class AdminExercisesController : ControllerBase
{
    private readonly IExerciseService _exerciseService;
    private readonly IExerciseVideoUploadService _exerciseVideoUploadService;

    public AdminExercisesController(
        IExerciseService exerciseService,
        IExerciseVideoUploadService exerciseVideoUploadService)
    {
        _exerciseService = exerciseService;
        _exerciseVideoUploadService = exerciseVideoUploadService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExerciseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExercises(
        [FromQuery] bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _exerciseService.GetAllAsync(isEnabled, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExerciseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExercise(Guid id, CancellationToken cancellationToken)
    {
        var result = await _exerciseService.GetByIdAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExerciseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateExercise(
        [FromBody] CreateExerciseDto request,
        CancellationToken cancellationToken)
    {
        var result = await _exerciseService.CreateAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetExercise), new { id = result.Value!.ExerciseId }, result.Value);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(ExerciseUploadLimits.MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ExerciseUploadLimits.MaxFileSizeBytes)]
    [ProducesResponseType(typeof(UploadExerciseVideoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadExerciseVideo(
        IFormFile file,
        [FromForm] string exerciseName,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Video file is required." } });
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

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ExerciseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateExercise(
        Guid id,
        [FromBody] UpdateExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _exerciseService.UpdateAsync(id, request, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExercise(Guid id, CancellationToken cancellationToken)
    {
        var result = await _exerciseService.DeleteAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateExercise(Guid id, CancellationToken cancellationToken)
    {
        var result = await _exerciseService.ActivateAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains("Exercise not found.")
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return NoContent();
    }
}
