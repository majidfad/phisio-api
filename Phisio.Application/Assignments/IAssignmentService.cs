using Phisio.Application.Admin.Assignments;
using Phisio.Application.Common;

namespace Phisio.Application.Assignments;

public interface IAssignmentService
{
    Task<AuthResult<AssignmentDto>> CreateAsync(
        Guid doctorId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<AssignmentDto>>> GetByPatientIdAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<AssignmentDto>>> GetMyAssignmentsAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> DeactivateAsync(
        Guid doctorId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<AssignmentReportDto>>> GetReportAsync(
        CancellationToken cancellationToken = default);
}
