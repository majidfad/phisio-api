using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;

namespace Phisio.Application.CarePlans;

public interface IExerciseProgramService
{
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
}
