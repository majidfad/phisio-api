using Phisio.Application.Common;

namespace Phisio.Application.Clinics;

public interface IClinicService
{
    Task<AuthResult<IReadOnlyList<ClinicDto>>> GetAllAsync(
        ClinicAccessContext access,
        bool isEnabled = true,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ClinicDto>> GetByIdAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ClinicDto>> CreateAsync(
        ClinicAccessContext access,
        CreateClinicDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ClinicDto>> UpdateAsync(
        ClinicAccessContext access,
        Guid clinicId,
        UpdateClinicDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ClinicDto>> ChangeManagerAsync(
        ClinicAccessContext access,
        Guid clinicId,
        ChangeClinicManagerDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>> GetDoctorsAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<ClinicPatientDto>>> GetPatientsAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid? doctorId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ClinicDoctorMemberDto>> AddDoctorAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RemoveDoctorAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<ClinicPhoneLookupResultDto>> LookupByPhonesAsync(
        ClinicAccessContext access,
        LookupClinicsByPhonesDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<AssignDoctorToClinicResultDto>> AssignDoctorAsync(
        ClinicAccessContext access,
        AssignDoctorToClinicDto request,
        CancellationToken cancellationToken = default);
}
