using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Common;
using Phisio.Application.Doctors;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class AdminDoctorService : IAdminDoctorService
{
    private const string DoctorRoleName = nameof(UserRole.Doctor);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AdminDoctorService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var doctors = await _userManager.Users
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .Where(user => user.Role == UserRole.Doctor)
            .OrderBy(user => user.Name)
            .ToListAsync(cancellationToken);

        if (doctors.Count == 0)
        {
            return AuthResult<IReadOnlyList<DoctorDto>>.Success([]);
        }

        var doctorIds = doctors.Select(doctor => doctor.Id).ToList();
        var profiles = await GetProfilesByDoctorIdsAsync(doctorIds, isEnabled, cancellationToken);

        var result = doctors
            .Select(doctor => MapToDto(doctor, profiles.GetValueOrDefault(doctor.Id)))
            .ToList();

        return AuthResult<IReadOnlyList<DoctorDto>>.Success(result);
    }

    public async Task<AuthResult<DoctorDto>> GetByIdAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await FindDoctorAsync(doctorId, cancellationToken);

        if (doctor is null)
        {
            return AuthResult<DoctorDto>.Failure(["Doctor not found."]);
        }

        var profile = await _dbContext.DoctorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

        return AuthResult<DoctorDto>.Success(MapToDto(doctor, profile));
    }

    public async Task<AuthResult<DoctorDto>> CreateAsync(
        CreateAdminDoctorDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureDoctorRoleExistsAsync(cancellationToken);

        var validationError = await ValidateUniqueCredentialsAsync(
            request.PhoneNumber,
            request.Email,
            request.MedicalLicenseNumber,
            excludeUserId: null,
            excludeDoctorId: null,
            cancellationToken);

        if (validationError is not null)
        {
            return AuthResult<DoctorDto>.Failure([validationError]);
        }

        var doctor = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Role = UserRole.Doctor,
            CreatedAt = DateTime.UtcNow,
        };

        UserCredentials.Apply(doctor, request.PhoneNumber, request.Email);

        var createResult = await _userManager.CreateAsync(doctor, TemporaryPasswordGenerator.Generate());

        if (!createResult.Succeeded)
        {
            return AuthResult<DoctorDto>.Failure(
                createResult.Errors.Select(error => error.Description));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(doctor, DoctorRoleName);

        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(doctor);
            return AuthResult<DoctorDto>.Failure(
                addRoleResult.Errors.Select(error => error.Description));
        }

        var profile = CreateProfile(doctor.Id, request);
        _dbContext.DoctorProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<DoctorDto>.Success(MapToDto(doctor, profile));
    }

    public async Task<AuthResult<DoctorDto>> UpdateAsync(
        Guid doctorId,
        UpdateAdminDoctorDto request,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _userManager.FindByIdAsync(doctorId.ToString());

        if (doctor is null || doctor.Role != UserRole.Doctor)
        {
            return AuthResult<DoctorDto>.Failure(["Doctor not found."]);
        }

        var profile = await _dbContext.DoctorProfiles
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

        var validationError = await ValidateUniqueCredentialsAsync(
            request.PhoneNumber,
            request.Email,
            request.MedicalLicenseNumber,
            excludeUserId: doctorId,
            excludeDoctorId: profile?.DoctorProfileId,
            cancellationToken);

        if (validationError is not null)
        {
            return AuthResult<DoctorDto>.Failure([validationError]);
        }

        doctor.Name = request.Name.Trim();
        UserCredentials.Apply(doctor, request.PhoneNumber, request.Email);

        var updateResult = await _userManager.UpdateAsync(doctor);

        if (!updateResult.Succeeded)
        {
            return AuthResult<DoctorDto>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        if (profile is null)
        {
            profile = CreateProfile(doctor.Id, request);
            _dbContext.DoctorProfiles.Add(profile);
        }
        else
        {
            ApplyProfileFields(profile, request);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<DoctorDto>.Success(MapToDto(doctor, profile));
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _userManager.Users
            .FirstOrDefaultAsync(
                user => user.Id == doctorId && user.Role == UserRole.Doctor,
                cancellationToken);

        if (doctor is null)
        {
            return AuthResult<bool>.Failure(["Doctor not found."]);
        }

        var profile = await _dbContext.DoctorProfiles
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

        var assignments = await _dbContext.UserExercises
            .Where(assignment => assignment.DoctorId == doctorId)
            .ToListAsync(cancellationToken);

        SoftDeleteExtensions.SoftDeleteRange(assignments);

        if (profile is not null)
        {
            profile.SoftDelete();
        }

        doctor.SoftDelete();

        var updateResult = await _userManager.UpdateAsync(doctor);

        if (!updateResult.Succeeded)
        {
            return AuthResult<bool>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> ActivateAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                user => user.Id == doctorId && user.Role == UserRole.Doctor,
                cancellationToken);

        if (doctor is null)
        {
            return AuthResult<bool>.Failure(["Doctor not found."]);
        }

        if (doctor.IsEnabled)
        {
            return AuthResult<bool>.Failure(["Doctor is already active."]);
        }

        var profile = await _dbContext.DoctorProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

        doctor.IsEnabled = true;

        if (profile is not null)
        {
            profile.IsEnabled = true;
        }

        var updateResult = await _userManager.UpdateAsync(doctor);

        if (!updateResult.Succeeded)
        {
            return AuthResult<bool>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    private async Task<ApplicationUser?> FindDoctorAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        return await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == doctorId && user.Role == UserRole.Doctor,
                cancellationToken);
    }

    private async Task<Dictionary<Guid, DoctorProfile>> GetProfilesByDoctorIdsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        if (doctorIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.DoctorProfiles
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .Where(profile => doctorIds.Contains(profile.DoctorId))
            .ToDictionaryAsync(profile => profile.DoctorId, cancellationToken);
    }

    private static DoctorProfile CreateProfile(Guid doctorId, CreateAdminDoctorDto request) =>
        CreateProfile(
            doctorId,
            request.Specialty,
            request.MedicalLicenseNumber,
            request.ClinicAddress);

    private static DoctorProfile CreateProfile(Guid doctorId, UpdateAdminDoctorDto request) =>
        CreateProfile(
            doctorId,
            request.Specialty,
            request.MedicalLicenseNumber,
            request.ClinicAddress);

    private static DoctorProfile CreateProfile(
        Guid doctorId,
        string specialty,
        string medicalLicenseNumber,
        string clinicAddress) =>
        new()
        {
            DoctorProfileId = Guid.NewGuid(),
            DoctorId = doctorId,
            Specialty = specialty.Trim(),
            MedicalLicenseNumber = medicalLicenseNumber.Trim(),
            ClinicAddress = clinicAddress.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

    private static void ApplyProfileFields(DoctorProfile profile, UpdateAdminDoctorDto request)
    {
        profile.Specialty = request.Specialty.Trim();
        profile.MedicalLicenseNumber = request.MedicalLicenseNumber.Trim();
        profile.ClinicAddress = request.ClinicAddress.Trim();
    }

    private static DoctorDto MapToDto(ApplicationUser doctor, DoctorProfile? profile) =>
        new(
            doctor.Id,
            doctor.Name,
            doctor.PhoneNumber ?? string.Empty,
            profile?.Specialty ?? string.Empty,
            profile?.MedicalLicenseNumber ?? string.Empty,
            profile?.ClinicAddress ?? string.Empty,
            profile?.CreatedAt ?? doctor.CreatedAt,
            doctor.Email,
            doctor.IsEnabled);

    private async Task<string?> ValidateUniqueCredentialsAsync(
        string phoneNumber,
        string? email,
        string medicalLicenseNumber,
        Guid? excludeUserId,
        Guid? excludeDoctorId,
        CancellationToken cancellationToken)
    {
        var lookupValues = UserCredentials.GetPhoneLookupValues(phoneNumber);

        var phoneInUse = await _userManager.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                user => user.Id != excludeUserId
                    && (lookupValues.Contains(user.PhoneNumber!)
                        || lookupValues.Contains(user.UserName!)),
                cancellationToken);

        if (phoneInUse)
        {
            return "Phone number is already registered.";
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingByEmail = await _userManager.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    user => user.NormalizedEmail == email.Trim().ToUpperInvariant(),
                    cancellationToken);

            if (existingByEmail is not null && existingByEmail.Id != excludeUserId)
            {
                return "Email is already registered.";
            }
        }

        var trimmedLicense = medicalLicenseNumber.Trim();

        if (!string.IsNullOrEmpty(trimmedLicense))
        {
            var licenseInUse = await _dbContext.DoctorProfiles
                .IgnoreQueryFilters()
                .AnyAsync(
                    profile => profile.MedicalLicenseNumber == trimmedLicense
                        && profile.DoctorProfileId != excludeDoctorId,
                    cancellationToken);

            if (licenseInUse)
            {
                return "Medical license number is already registered.";
            }
        }

        return null;
    }

    private async Task EnsureDoctorRoleExistsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await _roleManager.RoleExistsAsync(DoctorRoleName))
        {
            return;
        }

        var createRoleResult = await _roleManager.CreateAsync(
            new ApplicationRole { Id = Guid.NewGuid(), Name = DoctorRoleName });

        if (!createRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create role '{DoctorRoleName}': {string.Join(", ", createRoleResult.Errors.Select(error => error.Description))}");
        }
    }
}
