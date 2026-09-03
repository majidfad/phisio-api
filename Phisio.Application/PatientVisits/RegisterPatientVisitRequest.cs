using Phisio.Domain.Enums;

namespace Phisio.Application.PatientVisits;

public sealed record RegisterPatientVisitRequest(
    Guid PatientId,
    Guid DoctorId,
    Guid ClinicId,
    DateTime VisitAt,
    VisitType? VisitType,
    PatientCondition? PatientCondition,
    string? DoctorNotes);

