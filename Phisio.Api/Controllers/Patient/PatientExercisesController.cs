using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.PatientExercises;

namespace Phisio.Api.Controllers.Patient;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/patient/exercises")]
public class PatientExercisesController : ControllerBase
{
    private readonly IPatientExerciseService _patientExerciseService;

    public PatientExercisesController(IPatientExerciseService patientExerciseService)
    {
        _patientExerciseService = patientExerciseService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PatientExercisesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExercises(
        [FromQuery] DateOnly? scheduledDate = null,
        [FromQuery] Guid? doctorId = null,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientExerciseService.GetExercisesAsync(
            patientId.Value,
            scheduledDate,
            doctorId,
            cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("today")]
    [ProducesResponseType(typeof(PatientTodayExercisesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTodayExercises(
        [FromQuery] Guid? doctorId = null,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientExerciseService.GetTodayExercisesAsync(
            patientId.Value,
            doctorId,
            cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(CompleteExercisesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteExercises(
        [FromBody] CompleteExercisesRequest request,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientExerciseService.CompleteExercisesAsync(
            patientId.Value,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            if (result.Errors.Contains(PatientExerciseErrors.AssignmentNotFound))
            {
                return NotFound(new { errors = result.Errors });
            }

            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
