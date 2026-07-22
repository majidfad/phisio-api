using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.PatientDoctors;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PatientDoctorService : IPatientDoctorService
{
    private readonly AppDbContext _dbContext;

    public PatientDoctorService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<PatientDoctorDirectoryItemDto>>> SearchDoctorsAsync(
        Guid patientId,
        string? search,
        string? specialty,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
        var normalizedSpecialty = string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim().ToLowerInvariant();

        var relationships = await _dbContext.DoctorPatients
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(dp => dp.PatientId == patientId && dp.IsEnabled)
            .Select(dp => new { dp.DoctorId, dp.Status })
            .ToListAsync(cancellationToken);

        var statusByDoctorId = relationships.ToDictionary(item => item.DoctorId, item => item.Status);

        var query =
            from doctor in _dbContext.Users.AsNoTracking()
            join profile in _dbContext.DoctorProfiles.AsNoTracking()
                on doctor.Id equals profile.DoctorId into profiles
            from profile in profiles.DefaultIfEmpty()
            where doctor.Role == UserRole.Doctor
                && doctor.IsEnabled
                && (profile == null || profile.IsEnabled)
            select new
            {
                Doctor = doctor,
                Profile = profile,
            };

        if (normalizedSearch is not null)
        {
            query = query.Where(item =>
                item.Doctor.Name.ToLower().Contains(normalizedSearch)
                || (item.Profile != null && item.Profile.Specialty.ToLower().Contains(normalizedSearch))
                || (item.Profile != null && item.Profile.ClinicAddress.ToLower().Contains(normalizedSearch))
                || (item.Profile != null && item.Profile.MedicalLicenseNumber.ToLower().Contains(normalizedSearch)));
        }

        if (normalizedSpecialty is not null)
        {
            query = query.Where(item =>
                item.Profile != null && item.Profile.Specialty.ToLower() == normalizedSpecialty);
        }

        var doctors = await query
            .OrderBy(item => item.Doctor.Name)
            .ToListAsync(cancellationToken);

        var result = doctors
            .Select(item => new PatientDoctorDirectoryItemDto(
                item.Doctor.Id,
                item.Doctor.Name,
                item.Profile?.Specialty ?? string.Empty,
                item.Profile?.MedicalLicenseNumber ?? string.Empty,
                item.Profile?.ClinicAddress ?? string.Empty,
                item.Doctor.PhoneNumber ?? string.Empty,
                statusByDoctorId.TryGetValue(item.Doctor.Id, out var status) ? status : null))
            .ToList();

        return AuthResult<IReadOnlyList<PatientDoctorDirectoryItemDto>>.Success(result);
    }

    public async Task<AuthResult<PatientDoctorProfileDto>> GetDoctorProfileAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await (
            from user in _dbContext.Users.AsNoTracking()
            join profile in _dbContext.DoctorProfiles.AsNoTracking()
                on user.Id equals profile.DoctorId into profiles
            from profile in profiles.DefaultIfEmpty()
            where user.Id == doctorId
                && user.Role == UserRole.Doctor
                && user.IsEnabled
                && (profile == null || profile.IsEnabled)
            select new { User = user, Profile = profile })
            .FirstOrDefaultAsync(cancellationToken);

        if (doctor is null)
        {
            return AuthResult<PatientDoctorProfileDto>.Failure([DoctorPatientErrors.DoctorNotFound]);
        }

        var relationship = await _dbContext.DoctorPatients
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.IsEnabled)
            .FirstOrDefaultAsync(cancellationToken);

        return AuthResult<PatientDoctorProfileDto>.Success(new PatientDoctorProfileDto(
            doctor.User.Id,
            doctor.User.Name,
            doctor.Profile?.Specialty ?? string.Empty,
            doctor.Profile?.MedicalLicenseNumber ?? string.Empty,
            doctor.Profile?.ClinicAddress ?? string.Empty,
            doctor.User.PhoneNumber ?? string.Empty,
            relationship?.Status,
            relationship?.CreatedAt));
    }

    public async Task<AuthResult<IReadOnlyList<PatientLinkedDoctorDto>>> GetMyDoctorsAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var doctors = await (
            from dp in _dbContext.DoctorPatients.AsNoTracking()
            join doctor in _dbContext.Users.AsNoTracking() on dp.DoctorId equals doctor.Id
            join profile in _dbContext.DoctorProfiles.AsNoTracking()
                on doctor.Id equals profile.DoctorId into profiles
            from profile in profiles.DefaultIfEmpty()
            where dp.PatientId == patientId
                && dp.IsEnabled
                && (dp.Status == DoctorPatientStatus.Pending || dp.Status == DoctorPatientStatus.Approved)
                && doctor.Role == UserRole.Doctor
                && doctor.IsEnabled
            orderby dp.Status, doctor.Name
            select new PatientLinkedDoctorDto(
                doctor.Id,
                doctor.Name,
                profile != null ? profile.Specialty : string.Empty,
                profile != null ? profile.MedicalLicenseNumber : string.Empty,
                profile != null ? profile.ClinicAddress : string.Empty,
                doctor.PhoneNumber ?? string.Empty,
                dp.Status,
                dp.CreatedAt))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<PatientLinkedDoctorDto>>.Success(doctors);
    }

    public async Task<AuthResult<PatientLinkedDoctorDto>> RequestLinkAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await (
            from user in _dbContext.Users.AsNoTracking()
            join profile in _dbContext.DoctorProfiles.AsNoTracking()
                on user.Id equals profile.DoctorId into profiles
            from profile in profiles.DefaultIfEmpty()
            where user.Id == doctorId
                && user.Role == UserRole.Doctor
                && user.IsEnabled
            select new { User = user, Profile = profile })
            .FirstOrDefaultAsync(cancellationToken);

        if (doctor is null)
        {
            return AuthResult<PatientLinkedDoctorDto>.Failure([DoctorPatientErrors.DoctorNotFound]);
        }

        var existing = await _dbContext.DoctorPatients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (existing is { IsEnabled: true, Status: DoctorPatientStatus.Approved })
        {
            return AuthResult<PatientLinkedDoctorDto>.Failure([DoctorPatientErrors.AlreadyApproved]);
        }

        if (existing is { IsEnabled: true, Status: DoctorPatientStatus.Pending })
        {
            return AuthResult<PatientLinkedDoctorDto>.Failure([DoctorPatientErrors.AlreadyRequested]);
        }

        var now = DateTime.UtcNow;

        if (existing is not null)
        {
            existing.IsEnabled = true;
            existing.Status = DoctorPatientStatus.Pending;
            existing.CreatedAt = now;
        }
        else
        {
            _dbContext.DoctorPatients.Add(new DoctorPatient
            {
                DoctorId = doctorId,
                PatientId = patientId,
                Status = DoctorPatientStatus.Pending,
                IsEnabled = true,
                CreatedAt = now,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<PatientLinkedDoctorDto>.Success(new PatientLinkedDoctorDto(
            doctor.User.Id,
            doctor.User.Name,
            doctor.Profile?.Specialty ?? string.Empty,
            doctor.Profile?.MedicalLicenseNumber ?? string.Empty,
            doctor.Profile?.ClinicAddress ?? string.Empty,
            doctor.User.PhoneNumber ?? string.Empty,
            DoctorPatientStatus.Pending,
            now));
    }

    public async Task<AuthResult<bool>> CancelRequestAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([DoctorPatientErrors.RequestNotFound]);
        }

        relationship.IsEnabled = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> UnlinkAsync(
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WhereActive()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([DoctorPatientErrors.NotApproved]);
        }

        relationship.IsEnabled = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }
}
