using Phisio.Domain.Enums;

namespace Phisio.Application.PatientVisits;

public sealed record PatientVisitDto(
    Guid VisitId,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid ClinicId,
    string ClinicName,
    DateTime VisitAt,
    VisitType? VisitType,
    PatientCondition? PatientCondition,
    string? DoctorNotes);

public sealed record PatientVisitHistoryResponse(
    IReadOnlyList<PatientVisitDto> Visits,
    int TotalVisits,
    int Page,
    int PageSize);

