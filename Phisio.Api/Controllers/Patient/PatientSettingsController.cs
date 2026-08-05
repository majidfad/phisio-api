using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.PatientSettings;

namespace Phisio.Api.Controllers.Patient;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/patient/settings")]
public class PatientSettingsController : ControllerBase
{
    private readonly IPatientSettingsService _patientSettingsService;

    public PatientSettingsController(IPatientSettingsService patientSettingsService)
    {
        _patientSettingsService = patientSettingsService;
    }

    [HttpGet("reminders")]
    [ProducesResponseType(typeof(PatientReminderSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReminderSettings(CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientSettingsService.GetReminderSettingsAsync(
            patientId.Value,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPut("reminders")]
    [ProducesResponseType(typeof(PatientReminderSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReminderSettings(
        [FromBody] UpdatePatientReminderSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientSettingsService.UpdateReminderSettingsAsync(
            patientId.Value,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            var notFound = result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
            return notFound
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
