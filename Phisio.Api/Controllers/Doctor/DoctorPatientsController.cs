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

    [HttpGet("requests")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorPatientRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingRequests(CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPendingRequestsAsync(doctorId.Value, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{patientId:guid}/approve")]
    [ProducesResponseType(typeof(DoctorPatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveRequest(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.ApproveRequestAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(DoctorPatientErrors.RequestNotFound)
                || result.Errors.Contains(DoctorPatientErrors.PatientNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("{patientId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectRequest(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.RejectRequestAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
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
            page,
            pageSize,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpGet("{patientId:guid}/overview")]
    [ProducesResponseType(typeof(DoctorPatientOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientOverview(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPatientOverviewAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpGet("{patientId:guid}/programs")]
    [ProducesResponseType(typeof(IReadOnlyList<ExerciseProgramDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientPrograms(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPatientProgramsAsync(
            doctorId.Value,
            patientId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("{patientId:guid}/programs")]
    [ProducesResponseType(typeof(CreateExerciseProgramResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePatientProgram(
        Guid patientId,
        [FromBody] CreateExerciseProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.CreateProgramAsync(
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

    [HttpPut("{patientId:guid}/programs/{programId:guid}")]
    [ProducesResponseType(typeof(CreateExerciseProgramResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePatientProgram(
        Guid patientId,
        Guid programId,
        [FromBody] UpdateExerciseProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.UpdateProgramAsync(
            doctorId.Value,
            patientId,
            programId,
            request,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(DoctorPatientErrors.PatientNotFound)
                || result.Errors.Contains(DoctorPatientErrors.ProgramNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{patientId:guid}/programs/{programId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePatientProgram(
        Guid patientId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.DeleteProgramAsync(
            doctorId.Value,
            patientId,
            programId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpGet("{patientId:guid}/exercise-stats")]
    [ProducesResponseType(typeof(PatientExerciseStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientExerciseStats(
        Guid patientId,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetExerciseStatsAsync(
            doctorId.Value,
            patientId,
            from,
            to,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }
}
