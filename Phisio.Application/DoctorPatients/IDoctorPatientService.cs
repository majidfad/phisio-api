using Phisio.Application.Common;

namespace Phisio.Application.DoctorPatients;

public interface IDoctorPatientService
{
    Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<DoctorPatientDto>> AddByPhoneAsync(
        Guid doctorId,
        AddDoctorPatientRequest request,
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
        CancellationToken cancellationToken = default);
}
