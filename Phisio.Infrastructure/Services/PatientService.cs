using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.Patients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PatientService : IPatientService
{
    private readonly AppDbContext _dbContext;

    public PatientService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<PatientDto>>> GetPatientsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var patients = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(dp => dp.DoctorId == doctorId)
            .Join(
                _dbContext.Users.AsNoTracking().Where(u => u.Role == UserRole.Patient),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new { dp, u })
            .OrderBy(x => x.u.Name)
            .Select(x => new PatientDto(
                x.u.Id,
                x.u.Name,
                x.u.PhoneNumber ?? string.Empty,
                x.dp.CreatedAt,
                x.u.Email,
                x.u.CreatedAt,
                Array.Empty<string>(),
                x.u.IsEnabled))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<PatientDto>>.Success(patients);
    }

    public async Task<AuthResult<PatientDto>> GetPatientByIdAsync(
        Guid doctorId,
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking().WhereActive()
            where dp.DoctorId == doctorId && dp.PatientId == patientId
            join u in _dbContext.Users.AsNoTracking() on dp.PatientId equals u.Id
            where u.Role == UserRole.Patient
            select new PatientDto(
                u.Id,
                u.Name,
                u.PhoneNumber ?? string.Empty,
                dp.CreatedAt,
                u.Email,
                u.CreatedAt,
                Array.Empty<string>(),
                u.IsEnabled))
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
        {
            return AuthResult<PatientDto>.Failure(
                ["Patient not found or is not linked to this doctor."]);
        }

        return AuthResult<PatientDto>.Success(patient);
    }
}
