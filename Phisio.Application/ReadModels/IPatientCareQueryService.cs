using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;

namespace Phisio.Application.ReadModels;

public interface IPatientCareQueryService
{
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

    Task<AuthResult<PatientExerciseStatsResponse>> GetExerciseStatsAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}
