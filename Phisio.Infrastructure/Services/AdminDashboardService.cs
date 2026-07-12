using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Dashboard;
using Phisio.Application.Common;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _dbContext;

    public AdminDashboardService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<AdminDashboardStatsDto>> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var doctorCount = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.Role == UserRole.Doctor, cancellationToken);

        var patientCount = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.Role == UserRole.Patient, cancellationToken);

        var exerciseCount = await _dbContext.Exercises
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return AuthResult<AdminDashboardStatsDto>.Success(
            new AdminDashboardStatsDto(doctorCount, patientCount, exerciseCount));
    }
}
