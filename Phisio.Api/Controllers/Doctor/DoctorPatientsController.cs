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
    public async Task<IActionResult> GetPatients(
        CancellationToken cancellationToken = default,
        [FromQuery] Guid? clinicId = null)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPatientsAsync(
            doctorId.Value,
            clinicId,
            cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("requests")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorPatientRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingRequests(
        CancellationToken cancellationToken = default,
        [FromQuery] Guid? clinicId = null)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetPendingRequestsAsync(
            doctorId.Value,
            clinicId,
            cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("clinics")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorClinicOptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyClinics(CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.GetMyClinicsAsync(doctorId.Value, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("lookup")]
    [ProducesResponseType(typeof(DoctorPatientLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupPatient(
        [FromQuery] string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.LookupPatientByPhoneAsync(
            doctorId.Value,
            phoneNumber,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(DoctorPatientErrors.PatientPhoneNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DoctorPatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPatient(
        [FromBody] AddDoctorPatientRequest request,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.AddPatientAsync(
            doctorId.Value,
            request.PatientId,
            request.ClinicId,
            cancellationToken);

        if (!result.Succeeded)
        {
            var statusCode = result.Errors.Contains(DoctorPatientErrors.PatientNotFound)
                || result.Errors.Contains(DoctorPatientErrors.ClinicNotFound)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new { errors = result.Errors });
        }

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
        [FromQuery] Guid clinicId,
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
            clinicId,
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
        [FromQuery] Guid clinicId,
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
            clinicId,
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
        [FromQuery] Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = User.GetUserId();
        if (doctorId is null)
        {
            return Unauthorized();
        }

        var result = await _doctorPatientService.RemoveAsync(
            doctorId.Value,
            patientId,
            clinicId,
            cancellationToken);

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
        [FromQuery] Guid clinicId,
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
            clinicId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return CareError(result.Errors);
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
        [FromQuery] Guid clinicId,
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
            clinicId,
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
        [FromQuery] Guid clinicId,
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
            clinicId,
            page,
            pageSize,
            cancellationToken);

        if (!result.Succeeded)
        {
            return CareError(result.Errors);
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
        [FromQuery] Guid clinicId,
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
            clinicId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return CareError(result.Errors);
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
        [FromQuery] Guid clinicId,
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
            clinicId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return CareError(result.Errors);
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
        [FromQuery] Guid clinicId,
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
            clinicId,
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
        [FromQuery] Guid clinicId,
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
            clinicId,
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
        [FromQuery] Guid clinicId,
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
            clinicId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return CareError(result.Errors);
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
        [FromQuery] Guid clinicId,
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
            clinicId,
            from,
            to,
            cancellationToken);

        if (!result.Succeeded)
        {
            return CareError(result.Errors);
        }

        return Ok(result.Value);
    }

    private IActionResult CareError(IReadOnlyList<string> errors)
    {
        if (errors.Contains(DoctorPatientErrors.ClinicRequired))
        {
            return BadRequest(new { errors });
        }

        return NotFound(new { errors });
    }
}
