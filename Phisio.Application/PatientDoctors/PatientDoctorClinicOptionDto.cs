using Phisio.Domain.Enums;

namespace Phisio.Application.PatientDoctors;

public sealed record PatientDoctorClinicOptionDto(
    Guid ClinicId,
    string Name,
    string Address,
    DoctorPatientStatus? RelationshipStatus);
