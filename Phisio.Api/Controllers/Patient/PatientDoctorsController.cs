using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDoctors;

namespace Phisio.Api.Controllers.Patient;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/patient/doctors")]
public class PatientDoctorsController : ControllerBase
{
    private readonly IPatientDoctorService _patientDoctorService;

    public PatientDoctorsController(IPatientDoctorService patientDoctorService)
    {
        _patientDoctorService = patientDoctorService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatientDoctorDirectoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchDoctors(
        [FromQuery] string? search = null,
        [FromQuery] string? specialty = null,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDoctorService.SearchDoctorsAsync(
            patientId.Value,
            search,
            specialty,
            cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientLinkedDoctorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyDoctors(CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDoctorService.GetMyDoctorsAsync(patientId.Value, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{doctorId:guid}")]
    [ProducesResponseType(typeof(PatientDoctorProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDoctorProfile(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDoctorService.GetDoctorProfileAsync(
            patientId.Value,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("{doctorId:guid}/request")]
    [ProducesResponseType(typeof(PatientLinkedDoctorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestLink(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDoctorService.RequestLinkAsync(
            patientId.Value,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(DoctorPatientErrors.DoctorNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{doctorId:guid}/request")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelRequest(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDoctorService.CancelRequestAsync(
            patientId.Value,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpDelete("{doctorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlink(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDoctorService.UnlinkAsync(
            patientId.Value,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }
}
