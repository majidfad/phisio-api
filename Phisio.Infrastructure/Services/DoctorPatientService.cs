using Phisio.Application.CareDelivery;
using Phisio.Application.CarePlans;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.ReadModels;
using Phisio.Application.Relationships;

namespace Phisio.Infrastructure.Services;

/// <summary>
/// Facade over care-relationship, assignment, program, and query services.
/// Keeps the existing <see cref="IDoctorPatientService"/> API stable for controllers.
/// </summary>
public class DoctorPatientService : IDoctorPatientService
{
    public const string PatientNotFoundError = DoctorPatientErrors.PatientNotFound;
    public const string RelationshipNotFoundError = DoctorPatientErrors.RelationshipNotFound;
    public const string RequestNotFoundError = DoctorPatientErrors.RequestNotFound;
    public const string NoExercisesSelectedError = DoctorPatientErrors.NoExercisesSelected;
    public const string NoDatesSelectedError = DoctorPatientErrors.NoDatesSelected;
    public const string NoValidExercisesError = DoctorPatientErrors.NoValidExercises;

    private readonly ICareRelationshipService _careRelationships;
    private readonly IPatientCareAssignmentService _assignments;
    private readonly IExerciseProgramService _programs;
    private readonly IPatientCareQueryService _queries;

    public DoctorPatientService(
        ICareRelationshipService careRelationships,
        IPatientCareAssignmentService assignments,
        IExerciseProgramService programs,
        IPatientCareQueryService queries)
    {
        _careRelationships = careRelationships;
        _assignments = assignments;
        _programs = programs;
        _queries = queries;
    }

    public Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default) =>
        _careRelationships.GetPatientsAsync(doctorId, clinicId, cancellationToken);

    public Task<AuthResult<IReadOnlyList<DoctorPatientRequestDto>>> GetPendingRequestsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default) =>
        _careRelationships.GetPendingRequestsAsync(doctorId, clinicId, cancellationToken);

    public Task<AuthResult<IReadOnlyList<DoctorClinicOptionDto>>> GetMyClinicsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default) =>
        _careRelationships.GetMyClinicsAsync(doctorId, cancellationToken);

    public Task<AuthResult<DoctorPatientLookupDto>> LookupPatientByPhoneAsync(
        Guid doctorId,
        string? phoneNumber,
        CancellationToken cancellationToken = default) =>
        _careRelationships.LookupPatientByPhoneAsync(doctorId, phoneNumber, cancellationToken);

    public Task<AuthResult<DoctorPatientDto>> AddPatientAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _careRelationships.AddPatientAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _careRelationships.ApproveRequestAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _careRelationships.RejectRequestAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _careRelationships.RemoveAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>> GetPatientExercisesAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _assignments.GetPatientExercisesAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<AssignPatientExercisesResultDto>> AssignExercisesAsync(
        Guid doctorId,
        Guid patientId,
        AssignPatientExercisesRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _assignments.AssignExercisesAsync(doctorId, patientId, request, clinicId, cancellationToken);

    public Task<AuthResult<PatientExerciseHistoryResponse>> GetExerciseHistoryAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        _queries.GetExerciseHistoryAsync(doctorId, patientId, clinicId, page, pageSize, cancellationToken);

    public Task<AuthResult<DoctorPatientOverviewDto>> GetPatientOverviewAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _queries.GetPatientOverviewAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<IReadOnlyList<ExerciseProgramDto>>> GetPatientProgramsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _programs.GetPatientProgramsAsync(doctorId, patientId, clinicId, cancellationToken);

    public Task<AuthResult<CreateExerciseProgramResultDto>> CreateProgramAsync(
        Guid doctorId,
        Guid patientId,
        CreateExerciseProgramRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _programs.CreateProgramAsync(doctorId, patientId, request, clinicId, cancellationToken);

    public Task<AuthResult<CreateExerciseProgramResultDto>> UpdateProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        UpdateExerciseProgramRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _programs.UpdateProgramAsync(doctorId, patientId, programId, request, clinicId, cancellationToken);

    public Task<AuthResult<bool>> DeleteProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        Guid clinicId,
        CancellationToken cancellationToken = default) =>
        _programs.DeleteProgramAsync(doctorId, patientId, programId, clinicId, cancellationToken);

    public Task<AuthResult<PatientExerciseStatsResponse>> GetExerciseStatsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        _queries.GetExerciseStatsAsync(doctorId, patientId, clinicId, from, to, cancellationToken);
}
