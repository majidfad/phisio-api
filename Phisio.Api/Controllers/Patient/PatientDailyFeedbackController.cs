using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Common;
using Phisio.Application.PatientDailyFeedback;

namespace Phisio.Api.Controllers.Patient;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.PatientOnly)]
[Route("api/patient/daily-feedback")]
public class PatientDailyFeedbackController : ControllerBase
{
    private readonly IPatientDailyFeedbackService _patientDailyFeedbackService;

    public PatientDailyFeedbackController(IPatientDailyFeedbackService patientDailyFeedbackService)
    {
        _patientDailyFeedbackService = patientDailyFeedbackService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubmitDailyFeedbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitFeedback(
        [FromBody] SubmitDailyFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var patientId = User.GetUserId();
        if (patientId is null)
        {
            return Unauthorized();
        }

        var result = await _patientDailyFeedbackService.SubmitAsync(
            patientId.Value,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(PatientDailyFeedbackErrors.DoctorNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
