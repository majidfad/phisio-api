using Phisio.Application.Common;

namespace Phisio.Application.DoctorPatients;

public interface IDoctorPatientService
{
    Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorPatientRequestDto>>> GetPendingRequestsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorClinicOptionDto>>> GetMyClinicsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientLookupDto>> LookupPatientByPhoneAsync(
        Guid doctorId,
        string? phoneNumber,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientDto>> AddPatientAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>> GetPatientExercisesAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<AssignPatientExercisesResultDto>> AssignExercisesAsync(
        Guid doctorId,
        Guid patientId,
        AssignPatientExercisesRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientExerciseHistoryResponse>> GetExerciseHistoryAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientOverviewDto>> GetPatientOverviewAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<ExerciseProgramDto>>> GetPatientProgramsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<CreateExerciseProgramResultDto>> CreateProgramAsync(
        Guid doctorId,
        Guid patientId,
        CreateExerciseProgramRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<CreateExerciseProgramResultDto>> UpdateProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        UpdateExerciseProgramRequest request,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientExerciseStatsResponse>> GetExerciseStatsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}
