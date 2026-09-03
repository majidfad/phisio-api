using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;

namespace Phisio.Application.CareDelivery;

public interface IPatientCareAssignmentService
{
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
}
