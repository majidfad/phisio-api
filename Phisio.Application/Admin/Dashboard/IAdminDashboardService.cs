using Phisio.Application.Common;

namespace Phisio.Application.Admin.Dashboard;

public interface IAdminDashboardService
{
    Task<AuthResult<AdminDashboardStatsDto>> GetStatsAsync(
        CancellationToken cancellationToken = default);
}
