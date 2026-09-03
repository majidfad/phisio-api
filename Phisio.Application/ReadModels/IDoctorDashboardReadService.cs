using Phisio.Application.Common;
using Phisio.Application.DoctorDashboard;

namespace Phisio.Application.ReadModels;

public interface IDoctorDashboardReadService
{
    Task<AuthResult<DoctorDashboardDto>> GetDashboardAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default);
}
