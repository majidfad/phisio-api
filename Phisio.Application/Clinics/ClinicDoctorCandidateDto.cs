namespace Phisio.Application.Clinics;

public sealed record ClinicDoctorCandidateDto(
    Guid DoctorId,
    string Name,
    string PhoneNumber,
    string Specialty,
    bool IsClinicManager);
