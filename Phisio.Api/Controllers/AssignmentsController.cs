using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Assignments;
using Phisio.Application.Common;

namespace Phisio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [Authorize(Policy = AuthorizationPolicies.DoctorOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] CreateAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _assignmentService.CreateAsync(doctorId.Value, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(
            nameof(GetPatientAssignments),
            new { patientId = result.Value!.PatientId },
            result.Value);
    }

    [Authorize(Policy = AuthorizationPolicies.DoctorOnly)]
    [HttpGet("patient/{patientId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientAssignments(
        Guid patientId,
        CancellationToken cancellationToken)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _assignmentService.GetByPatientIdAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = AuthorizationPolicies.PatientOnly)]
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyAssignments(CancellationToken cancellationToken)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _assignmentService.GetMyAssignmentsAsync(patientId.Value, cancellationToken);
        return Ok(result.Value);
    }

    [Authorize(Policy = AuthorizationPolicies.DoctorOnly)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAssignment(Guid id, CancellationToken cancellationToken)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _assignmentService.DeactivateAsync(doctorId.Value, id, cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase))
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return NoContent();
    }
}
