namespace Phisio.Application.Clinics;

public sealed record AssignDoctorToClinicResultDto(
    ClinicDto Clinic,
    ClinicDoctorMemberDto Doctor,
    bool ClinicCreated);
