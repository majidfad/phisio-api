using Phisio.Application.Common;
using Phisio.Application.DoctorDashboard;
using Phisio.Application.ReadModels;

namespace Phisio.Infrastructure.Services;

/// <summary>
/// Facade for dashboard queries. Delegates to the read model service.
/// </summary>
public class DoctorDashboardService : IDoctorDashboardService
{
    private readonly IDoctorDashboardReadService _readModel;

    public DoctorDashboardService(IDoctorDashboardReadService readModel)
    {
        _readModel = readModel;
    }

    public Task<AuthResult<DoctorDashboardDto>> GetDashboardAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default) =>
        _readModel.GetDashboardAsync(doctorId, clinicId, cancellationToken);
}
