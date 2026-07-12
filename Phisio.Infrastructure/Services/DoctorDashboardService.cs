using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorDashboard;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class DoctorDashboardService : IDoctorDashboardService
{
    private readonly AppDbContext _dbContext;

    public DoctorDashboardService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<DoctorDashboardDto>> GetDashboardAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patientsCount = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .CountAsync(dp => dp.DoctorId == doctorId, cancellationToken);

        var recentPatients = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(dp => dp.DoctorId == doctorId)
            .OrderByDescending(dp => dp.CreatedAt)
            .Take(5)
            .Join(
                _dbContext.Users.AsNoTracking().Where(u => u.Role == UserRole.Patient),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new DoctorDashboardRecentPatientDto(
                    u.Id,
                    u.Name,
                    u.PhoneNumber ?? string.Empty))
            .ToListAsync(cancellationToken);

        return AuthResult<DoctorDashboardDto>.Success(
            new DoctorDashboardDto(patientsCount, recentPatients));
    }
}
