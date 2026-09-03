using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.PatientVisits;
using Phisio.Domain.Enums;

namespace Phisio.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ClinicManagement)]
[Route("api/visits")]
public class VisitsController : ControllerBase
{
    private readonly IPatientVisitService _visitService;

    public VisitsController(IPatientVisitService visitService)
    {
        _visitService = visitService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PatientVisitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterVisit(
        [FromBody] RegisterPatientVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _visitService.RegisterVisitAsync(access, request, cancellationToken);
        if (!result.Succeeded)
        {
            return MapFailureForRegister(result.Errors);
        }

        return Ok(result.Value);
    }

    [HttpGet("patients/{patientId:guid}")]
    [ProducesResponseType(typeof(PatientVisitHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientVisits(
        Guid patientId,
        [FromQuery] Guid? clinicId = null,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _visitService.GetPatientVisitsAsync(
            access,
            patientId,
            clinicId,
            doctorId,
            page,
            pageSize,
            search,
            cancellationToken);

        if (!result.Succeeded)
        {
            return MapFailureForList(result.Errors);
        }

        return Ok(result.Value);
    }

    [HttpGet("patients/{patientId:guid}/recent")]
    [ProducesResponseType(typeof(PatientVisitDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMostRecentPatientVisit(
        Guid patientId,
        [FromQuery] Guid? clinicId = null,
        [FromQuery] Guid? doctorId = null,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _visitService.GetMostRecentPatientVisitAsync(
            access,
            patientId,
            clinicId,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return MapFailureForList(result.Errors);
        }

        return Ok(result.Value);
    }

    [HttpGet("clinics/{clinicId:guid}")]
    [ProducesResponseType(typeof(PatientVisitHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClinicVisits(
        Guid clinicId,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _visitService.GetClinicVisitsAsync(
            access,
            clinicId,
            doctorId,
            page,
            pageSize,
            search,
            cancellationToken);

        if (!result.Succeeded)
        {
            return MapFailureForList(result.Errors);
        }

        return Ok(result.Value);
    }

    [HttpGet("doctors/{doctorId:guid}")]
    [ProducesResponseType(typeof(PatientVisitHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDoctorVisits(
        Guid doctorId,
        [FromQuery] Guid? clinicId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _visitService.GetDoctorVisitsAsync(
            access,
            doctorId,
            clinicId,
            page,
            pageSize,
            search,
            cancellationToken);

        if (!result.Succeeded)
        {
            return MapFailureForList(result.Errors);
        }

        return Ok(result.Value);
    }

    private PatientVisitAccessContext? GetAccessContext()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return null;
        }

        UserRole role = User.IsInRole(RoleNames.Admin)
            ? UserRole.Admin
            : User.IsInRole(RoleNames.ClinicManager)
                ? UserRole.ClinicManager
                : User.IsInRole(RoleNames.Doctor)
                    ? UserRole.Doctor
                    : UserRole.Patient;

        return new PatientVisitAccessContext(userId.Value, role);
    }

    private IActionResult MapFailureForRegister(IReadOnlyList<string> errors)
    {
        if (errors.Contains(PatientVisitErrors.ClinicManagerNotAuthorized)
            || errors.Contains(PatientVisitErrors.DoctorMismatch))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { errors });
        }

        if (errors.Contains(PatientVisitErrors.PatientNotFound)
            || errors.Contains(PatientVisitErrors.DoctorNotFound)
            || errors.Contains(PatientVisitErrors.ClinicNotFound))
        {
            return NotFound(new { errors });
        }

        // Relationship / validation type errors.
        return BadRequest(new { errors });
    }

    private IActionResult MapFailureForList(IReadOnlyList<string> errors)
    {
        if (errors.Contains(PatientVisitErrors.ClinicManagerNotAuthorized)
            || errors.Contains(PatientVisitErrors.DoctorMismatch))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { errors });
        }

        if (errors.Contains(PatientVisitErrors.PatientNotFound)
            || errors.Contains(PatientVisitErrors.PatientNotConnectedToDoctor)
            || errors.Contains(PatientVisitErrors.ClinicNotFound)
            || errors.Contains(PatientVisitErrors.DoctorNotFound))
        {
            return NotFound(new { errors });
        }

        return BadRequest(new { errors });
    }
}

