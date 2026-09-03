using Phisio.Application.Common;

namespace Phisio.Application.PatientVisits;

public interface IPatientVisitService
{
    Task<AuthResult<PatientVisitDto>> RegisterVisitAsync(
        PatientVisitAccessContext access,
        RegisterPatientVisitRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientVisitHistoryResponse>> GetPatientVisitsAsync(
        PatientVisitAccessContext access,
        Guid patientId,
        Guid? clinicId,
        Guid? doctorId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientVisitDto?>> GetMostRecentPatientVisitAsync(
        PatientVisitAccessContext access,
        Guid patientId,
        Guid? clinicId,
        Guid? doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientVisitHistoryResponse>> GetClinicVisitsAsync(
        PatientVisitAccessContext access,
        Guid clinicId,
        Guid? doctorId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientVisitHistoryResponse>> GetDoctorVisitsAsync(
        PatientVisitAccessContext access,
        Guid doctorId,
        Guid? clinicId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<AuthResult<VisitFeedbackDto>> SubmitVisitFeedbackAsync(
        PatientVisitAccessContext access,
        Guid visitId,
        SubmitVisitFeedbackRequest request,
        CancellationToken cancellationToken = default);
}

