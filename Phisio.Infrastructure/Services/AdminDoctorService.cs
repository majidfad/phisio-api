using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin;
using Phisio.Application.Admin.Doctors;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Application.Doctors;
using Phisio.Application.Notifications;
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
    private readonly IClinicService _clinicService;
    private readonly INotificationService _notifications;

    public AdminDoctorService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IClinicService clinicService,
        INotificationService? notifications = null)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _clinicService = clinicService;
        _notifications = notifications ?? NoOpNotificationService.Instance;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var doctors = await _userManager.Users
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .Where(user => user.Role == UserRole.Doctor || user.Role == UserRole.ClinicManager)
            .OrderBy(user => user.Name)
            .ToListAsync(cancellationToken);

        if (doctors.Count == 0)
        {
            return AuthResult<IReadOnlyList<DoctorDto>>.Success([]);
        }

        var doctorIds = doctors.Select(doctor => doctor.Id).ToList();
        var profiles = await GetProfilesByDoctorIdsAsync(doctorIds, isEnabled, cancellationToken);
        var managedClinics = await GetManagedClinicNamesByUserIdsAsync(doctorIds, cancellationToken);

        var result = doctors
            .Select(doctor => MapToDto(
                doctor,
                profiles.GetValueOrDefault(doctor.Id),
                managedClinics.GetValueOrDefault(doctor.Id)))
            .ToList();

        return AuthResult<IReadOnlyList<DoctorDto>>.Success(result);
    }

    public async Task<AuthResult<DoctorDto>> GetByIdAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var doctor = await FindDoctorOrClinicManagerAsync(doctorId, cancellationToken);

        if (doctor is null)
        {
            return AuthResult<DoctorDto>.Failure(["Doctor not found."]);
        }

        var profile = await _dbContext.DoctorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);
        var managedClinics = await GetManagedClinicNamesByUserIdsAsync([doctorId], cancellationToken);

        return AuthResult<DoctorDto>.Success(
            MapToDto(doctor, profile, managedClinics.GetValueOrDefault(doctorId)));
    }

    public async Task<AuthResult<CreateAdminDoctorResponse>> CreateAsync(
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
            return AuthResult<CreateAdminDoctorResponse>.Failure([validationError]);
        }

        var adminAccess = new ClinicAccessContext(Guid.Empty, IsAdmin: true);
        var clinicLookup = await _clinicService.LookupByPhonesAsync(
            adminAccess,
            new LookupClinicsByPhonesDto { PhoneNumbers = request.ClinicPhoneNumbers },
            cancellationToken);

        if (!clinicLookup.Succeeded)
        {
            return AuthResult<CreateAdminDoctorResponse>.Failure(clinicLookup.Errors);
        }

        if (clinicLookup.Value!.Status == ClinicPhoneLookupStatus.Conflict)
        {
            return AuthResult<CreateAdminDoctorResponse>.Failure(
                [ClinicErrors.ConflictingClinicPhones]);
        }

        var creatingNewClinic = clinicLookup.Value.Status == ClinicPhoneLookupStatus.None;
        if (creatingNewClinic)
        {
            if (string.IsNullOrWhiteSpace(request.NewClinicName)
                || string.IsNullOrWhiteSpace(request.NewClinicAddress))
            {
                return AuthResult<CreateAdminDoctorResponse>.Failure(
                    [ClinicErrors.ClinicCreateDetailsRequired]);
            }

            if (!request.ManagerIsThisDoctor
                && (request.ClinicManagerId is null || request.ClinicManagerId == Guid.Empty))
            {
                return AuthResult<CreateAdminDoctorResponse>.Failure(
                    [ClinicErrors.ManagerIdRequired]);
            }
        }

        var profileAddress = ResolveClinicAddressForProfile(request, clinicLookup.Value);
        if (string.IsNullOrWhiteSpace(profileAddress))
        {
            return AuthResult<CreateAdminDoctorResponse>.Failure(
                [ClinicErrors.ClinicCreateDetailsRequired]);
        }

        var (password, wasGenerated) = AdminPasswordResolver.Resolve(
            request.Password,
            request.GeneratePassword);

        var doctor = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Role = UserRole.Doctor,
            CreatedAt = DateTime.UtcNow,
        };

        UserCredentials.Apply(doctor, request.PhoneNumber, request.Email);

        var createResult = await _userManager.CreateAsync(doctor, password);

        if (!createResult.Succeeded)
        {
            return AuthResult<CreateAdminDoctorResponse>.Failure(
                createResult.Errors.Select(error => error.Description));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(doctor, DoctorRoleName);

        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(doctor);
            return AuthResult<CreateAdminDoctorResponse>.Failure(
                addRoleResult.Errors.Select(error => error.Description));
        }

        var profile = CreateProfile(
            doctor.Id,
            request.Specialty,
            request.MedicalLicenseNumber,
            profileAddress);
        _dbContext.DoctorProfiles.Add(profile);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            var assignResult = await _clinicService.AssignDoctorAsync(
                adminAccess,
                new AssignDoctorToClinicDto
                {
                    DoctorId = doctor.Id,
                    PhoneNumbers = request.ClinicPhoneNumbers,
                    Name = request.NewClinicName,
                    Address = request.NewClinicAddress,
                    ManagerIsThisDoctor = request.ManagerIsThisDoctor,
                    ClinicManagerId = request.ClinicManagerId,
                },
                cancellationToken);

            if (!assignResult.Succeeded)
            {
                await _userManager.DeleteAsync(doctor);
                return AuthResult<CreateAdminDoctorResponse>.Failure(assignResult.Errors);
            }
        }
        catch
        {
            await _userManager.DeleteAsync(doctor);
            throw;
        }

        var managedClinics = await GetManagedClinicNamesByUserIdsAsync([doctor.Id], cancellationToken);

        return AuthResult<CreateAdminDoctorResponse>.Success(
            new CreateAdminDoctorResponse(
                MapToDto(doctor, profile, managedClinics.GetValueOrDefault(doctor.Id)),
                wasGenerated ? password : null));
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

        await _notifications.NotifyAsync(
            doctorId,
            NotificationType.DoctorActivated,
            "Account approved",
            "Your doctor account has been approved. You can now sign in.",
            new { doctorId, doctorName = doctor.Name },
            cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> DeactivateAsync(
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

        if (!doctor.IsEnabled)
        {
            return AuthResult<bool>.Failure(["Doctor is already inactive."]);
        }

        var profile = await _dbContext.DoctorProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

        doctor.IsEnabled = false;

        if (profile is not null)
        {
            profile.IsEnabled = false;
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

    public async Task<AuthResult<AdminSetPasswordResponse>> SetPasswordAsync(
        Guid doctorId,
        AdminSetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var doctor = await _userManager.FindByIdAsync(doctorId.ToString());

        if (doctor is null || doctor.Role != UserRole.Doctor)
        {
            return AuthResult<AdminSetPasswordResponse>.Failure(["Doctor not found."]);
        }

        var (password, wasGenerated) = AdminPasswordResolver.Resolve(
            request.Password,
            request.GeneratePassword);

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(doctor);
        var resetResult = await _userManager.ResetPasswordAsync(doctor, resetToken, password);

        if (!resetResult.Succeeded)
        {
            return AuthResult<AdminSetPasswordResponse>.Failure(
                resetResult.Errors.Select(error => error.Description));
        }

        return AuthResult<AdminSetPasswordResponse>.Success(
            new AdminSetPasswordResponse(
                "Password updated successfully.",
                wasGenerated ? password : null));
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

    private async Task<ApplicationUser?> FindDoctorOrClinicManagerAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        return await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == doctorId
                    && (user.Role == UserRole.Doctor || user.Role == UserRole.ClinicManager),
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

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetManagedClinicNamesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var clinics = await _dbContext.Clinics
            .AsNoTracking()
            .Where(clinic => userIds.Contains(clinic.ClinicManagerId))
            .Select(clinic => new { clinic.ClinicManagerId, clinic.Name })
            .ToListAsync(cancellationToken);

        return clinics
            .GroupBy(clinic => clinic.ClinicManagerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(clinic => clinic.Name)
                    .OrderBy(name => name)
                    .ToList());
    }

    private static string ResolveClinicAddressForProfile(
        CreateAdminDoctorDto request,
        ClinicPhoneLookupResultDto clinicLookup)
    {
        if (!string.IsNullOrWhiteSpace(request.ClinicAddress))
        {
            return request.ClinicAddress.Trim();
        }

        if (clinicLookup.Status == ClinicPhoneLookupStatus.Found
            && clinicLookup.Clinic is not null)
        {
            return clinicLookup.Clinic.Address;
        }

        return request.NewClinicAddress?.Trim() ?? string.Empty;
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

    private static DoctorDto MapToDto(
        ApplicationUser doctor,
        DoctorProfile? profile,
        IReadOnlyList<string>? managedClinicNames = null)
    {
        var clinics = managedClinicNames ?? [];
        var isClinicManager = doctor.Role == UserRole.ClinicManager || clinics.Count > 0;

        return new(
            doctor.Id,
            doctor.Name,
            doctor.PhoneNumber ?? string.Empty,
            profile?.Specialty ?? string.Empty,
            profile?.MedicalLicenseNumber ?? string.Empty,
            profile?.ClinicAddress ?? string.Empty,
            profile?.CreatedAt ?? doctor.CreatedAt,
            doctor.Email,
            doctor.IsEnabled,
            isClinicManager,
            clinics);
    }

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
