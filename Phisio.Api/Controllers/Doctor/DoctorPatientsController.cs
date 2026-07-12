using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;

namespace Phisio.Api.Controllers.Doctor;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.DoctorOnly)]
[Route("api/doctor/patients")]
public class DoctorPatientsController : ControllerBase
{
    private readonly IDoctorPatientService _doctorPatientService;

    public DoctorPatientsController(IDoctorPatientService doctorPatientService)
    {
        _doctorPatientService = doctorPatientService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorPatientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPatients(CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPatientsAsync(doctorId.Value, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("add")]
    [ProducesResponseType(typeof(DoctorPatientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddPatient(
        [FromBody] AddDoctorPatientRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.AddByPhoneAsync(doctorId.Value, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetPatients), result.Value);
    }

    [HttpDelete("{patientId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePatient(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.RemoveAsync(doctorId.Value, patientId, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpGet("{patientId:guid}/exercises")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorPatientExerciseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientExercises(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPatientExercisesAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("{patientId:guid}/exercises")]
    [ProducesResponseType(typeof(AssignPatientExercisesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignPatientExercises(
        Guid patientId,
        [FromBody] AssignPatientExercisesRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.AssignExercisesAsync(
            doctorId.Value,
            patientId,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(DoctorPatientErrors.PatientNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpGet("{patientId:guid}/exercise-history")]
    [ProducesResponseType(typeof(PatientExerciseHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientExerciseHistory(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetExerciseHistoryAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
