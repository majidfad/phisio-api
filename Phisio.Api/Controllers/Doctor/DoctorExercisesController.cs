using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Application.Common;
using Phisio.Application.DoctorExercises;

namespace Phisio.Api.Controllers.Doctor;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.DoctorOnly)]
[Route("api/doctor/exercises")]
public class DoctorExercisesController : ControllerBase
{
    private readonly IDoctorExerciseService _exerciseService;

    public DoctorExercisesController(IDoctorExerciseService exerciseService)
    {
        _exerciseService = exerciseService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorExerciseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExercises(CancellationToken cancellationToken = default)
    {
        var result = await _exerciseService.GetExercisesAsync(cancellationToken);
        return Ok(result.Value);
    }
}
