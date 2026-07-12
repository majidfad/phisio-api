using Phisio.Application.Common;

namespace Phisio.Application.DoctorDashboard;

public interface IDoctorDashboardService
{
    Task<AuthResult<DoctorDashboardDto>> GetDashboardAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default);
}
