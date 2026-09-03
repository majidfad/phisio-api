using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Clinics;
using Phisio.Application.Common;

namespace Phisio.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ClinicManagement)]
[Route("api/clinics")]
public class ClinicsController : ControllerBase
{
    private readonly IClinicService _clinicService;
    private readonly IAdminDoctorService _doctorService;

    public ClinicsController(IClinicService clinicService, IAdminDoctorService doctorService)
    {
        _clinicService = clinicService;
        _doctorService = doctorService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClinics(
        [FromQuery] bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetAllAsync(access, isEnabled, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClinic(Guid id, CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetByIdAsync(access, id, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Errors.Contains(ClinicErrors.NotFound)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateClinic(
        [FromBody] CreateClinicDto request,
        CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.CreateAsync(access, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetClinic), new { id = result.Value!.ClinicId }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClinic(
        Guid id,
        [FromBody] UpdateClinicDto request,
        CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.UpdateAsync(access, id, request, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Errors.Contains(ClinicErrors.NotFound)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/manager")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ClinicDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeClinicManager(
        Guid id,
        [FromBody] ChangeClinicManagerDto request,
        CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.ChangeManagerAsync(access, id, request, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Errors.Contains(ClinicErrors.NotFound)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClinic(Guid id, CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.DeleteAsync(access, id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpGet("doctor-candidates")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicDoctorCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDoctorCandidates(CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _doctorService.GetAllAsync(isEnabled: true, cancellationToken);

        var candidates = (result.Value ?? [])
            .Select(doctor => new ClinicDoctorCandidateDto(
                doctor.Id,
                doctor.Name,
                doctor.PhoneNumber,
                doctor.Specialty,
                doctor.IsClinicManager))
            .ToList();

        return Ok(candidates);
    }

    [HttpGet("{clinicId:guid}/doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicDoctorMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClinicDoctors(Guid clinicId, CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetDoctorsAsync(access, clinicId, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpGet("{clinicId:guid}/patients")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicPatientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClinicPatients(
        Guid clinicId,
        [FromQuery] Guid? doctorId = null,
        CancellationToken cancellationToken = default)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.GetPatientsAsync(
            access,
            clinicId,
            doctorId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("{clinicId:guid}/doctors/{doctorId:guid}")]
    [ProducesResponseType(typeof(ClinicDoctorMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddClinicDoctor(
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.AddDoctorAsync(access, clinicId, doctorId, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Errors.Contains(ClinicErrors.NotFound)
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(
            nameof(GetClinicDoctors),
            new { clinicId },
            result.Value);
    }

    [HttpPost("lookup-by-phones")]
    [ProducesResponseType(typeof(ClinicPhoneLookupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LookupClinicsByPhones(
        [FromBody] LookupClinicsByPhonesDto request,
        CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.LookupByPhonesAsync(access, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{clinicId:guid}/doctors/{doctorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveClinicDoctor(
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var access = GetAccessContext();
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await _clinicService.RemoveDoctorAsync(access, clinicId, doctorId, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Errors.Contains(ClinicErrors.CannotRemoveClinicManager)
                ? BadRequest(new { errors = result.Errors })
                : NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    private ClinicAccessContext? GetAccessContext()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return null;
        }

        return new ClinicAccessContext(userId.Value, User.IsInRole(RoleNames.Admin));
    }
}
