using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.PatientVisits;
using Phisio.Domain.Enums;

namespace Phisio.Api.Controllers.Patient;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/patient/visits")]
public class PatientVisitsController : ControllerBase
{
    private readonly IPatientVisitService _visitService;

    public PatientVisitsController(IPatientVisitService visitService)
    {
        _visitService = visitService;
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(PatientVisitHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyVisits(
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
            access.UserId,
            clinicId,
            doctorId,
            page,
            pageSize,
            search,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpGet("recent")]
    [ProducesResponseType(typeof(PatientVisitDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMostRecentVisit(
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
            access.UserId,
            clinicId,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
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

        return new PatientVisitAccessContext(userId.Value, UserRole.Patient);
    }
}

