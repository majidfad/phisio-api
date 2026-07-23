using Phisio.Application.Common;

namespace Phisio.Application.DoctorPatients;

public interface IDoctorPatientService
{
    Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorPatientRequestDto>>> GetPendingRequestsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<DoctorPatientExerciseDto>>> GetPatientExercisesAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<AssignPatientExercisesResultDto>> AssignExercisesAsync(
        Guid doctorId,
        Guid patientId,
        AssignPatientExercisesRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientExerciseHistoryResponse>> GetExerciseHistoryAsync(
        Guid doctorId,
        Guid patientId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientOverviewDto>> GetPatientOverviewAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<ExerciseProgramDto>>> GetPatientProgramsAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<CreateExerciseProgramResultDto>> CreateProgramAsync(
        Guid doctorId,
        Guid patientId,
        CreateExerciseProgramRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<CreateExerciseProgramResultDto>> UpdateProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        UpdateExerciseProgramRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeleteProgramAsync(
        Guid doctorId,
        Guid patientId,
        Guid programId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<PatientExerciseStatsResponse>> GetExerciseStatsAsync(
        Guid doctorId,
        Guid patientId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}
