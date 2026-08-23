using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Auth;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Application.Notifications;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services;

namespace Phisio.Infrastructure.Authentication;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IClinicService _clinicService;
    private readonly INotificationService _notifications;

    public AuthService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtTokenService jwtTokenService,
        IClinicService clinicService,
        INotificationService? notifications = null)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenService = jwtTokenService;
        _clinicService = clinicService;
        _notifications = notifications ?? NoOpNotificationService.Instance;
    }

    public async Task<AuthResult<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return AuthResult<RegisterResponse>.Failure([AuthErrorMessages.PasswordMismatch]);
        }

        return request.Role switch
        {
            UserRole.Patient => await RegisterPatientCoreAsync(
                request.Name,
                request.PhoneNumber,
                request.Password,
                cancellationToken),
            UserRole.Doctor => await RegisterDoctorCoreAsync(request, cancellationToken),
            _ => AuthResult<RegisterResponse>.Failure([AuthErrorMessages.InvalidRegistrationRole]),
        };
    }

    public async Task<AuthResult<RegisterPatientResponse>> RegisterPatientAsync(
        RegisterPatientRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await RegisterPatientCoreAsync(
            request.Name,
            request.PhoneNumber,
            request.Password,
            cancellationToken);

        if (!result.Succeeded)
        {
            return AuthResult<RegisterPatientResponse>.Failure(result.Errors);
        }

        var value = result.Value!;
        return AuthResult<RegisterPatientResponse>.Success(
            new RegisterPatientResponse(value.UserId, value.PhoneNumber, value.Name, value.Role));
    }

    public async Task<AuthResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByPhoneAsync(request.PhoneNumber, cancellationToken);
        if (user is null)
        {
            return AuthResult<AuthResponse>.Failure(["Invalid phone number or password."]);
        }

        if (!user.IsEnabled)
        {
            return user.Role.HasDoctorAccess()
                ? AuthResult<AuthResponse>.Failure([AuthErrorMessages.AccountNotApproved])
                : AuthResult<AuthResponse>.Failure(["This account has been disabled."]);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return AuthResult<AuthResponse>.Failure(["Invalid phone number or password."]);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tokenResult = _jwtTokenService.GenerateAccessToken(
            new AccessTokenGenerationRequest(
                user.Id,
                user.UserName!,
                user.Name,
                roles.Append(user.Role.ToString()).Distinct(StringComparer.OrdinalIgnoreCase)));
        var responseRole = roles.Contains(RoleNames.ClinicManager, StringComparer.OrdinalIgnoreCase)
            ? UserRole.ClinicManager
            : user.Role;

        return AuthResult<AuthResponse>.Success(
            new AuthResponse(
                tokenResult.AccessToken,
                tokenResult.ExpiresAt,
                user.Id,
                user.PhoneNumber!,
                user.Email,
                user.Name,
                responseRole));
    }

    public async Task<AuthResult<MeResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthResult<MeResponse>.Failure(["User not found."]);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return AuthResult<MeResponse>.Success(
            new MeResponse(user.Id, user.PhoneNumber!, user.Email, roles.ToList()));
    }

    public async Task<AuthResult<ChangePasswordResponse>> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.NewPassword != request.ConfirmPassword)
        {
            return AuthResult<ChangePasswordResponse>.Failure([AuthErrorMessages.PasswordMismatch]);
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            return AuthResult<ChangePasswordResponse>.Failure(
                [AuthErrorMessages.NewPasswordSameAsCurrent]);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthResult<ChangePasswordResponse>.Failure(["User not found."]);
        }

        var changeResult = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!changeResult.Succeeded)
        {
            return AuthResult<ChangePasswordResponse>.Failure(
                IdentityErrorLocalizer.Localize(changeResult.Errors));
        }

        return AuthResult<ChangePasswordResponse>.Success(
            new ChangePasswordResponse(AuthMessages.PasswordChanged));
    }

    private async Task<AuthResult<RegisterResponse>> RegisterPatientCoreAsync(
        string name,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(nameof(UserRole.Patient), cancellationToken);

        var validationError = await ValidateUniquePhoneAsync(
            phoneNumber,
            excludeUserId: null,
            cancellationToken);

        if (validationError is not null)
        {
            return AuthResult<RegisterResponse>.Failure([validationError]);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = name,
            Role = UserRole.Patient,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        UserCredentials.Apply(user, phoneNumber, email: null);

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return AuthResult<RegisterResponse>.Failure(
                IdentityErrorLocalizer.Localize(createResult.Errors));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, nameof(UserRole.Patient));
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return AuthResult<RegisterResponse>.Failure(
                IdentityErrorLocalizer.Localize(addRoleResult.Errors));
        }

        return AuthResult<RegisterResponse>.Success(
            new RegisterResponse(
                user.Id,
                user.PhoneNumber!,
                user.Name,
                user.Role,
                RegisterMessages.PatientRegistered));
    }

    private async Task<AuthResult<RegisterResponse>> RegisterDoctorCoreAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureRoleExistsAsync(nameof(UserRole.Doctor), cancellationToken);

        var validationError = await ValidateUniquePhoneAsync(
            request.PhoneNumber,
            excludeUserId: null,
            cancellationToken);

        if (validationError is not null)
        {
            return AuthResult<RegisterResponse>.Failure([validationError]);
        }

        var adminAccess = new ClinicAccessContext(Guid.Empty, IsAdmin: true);
        var clinicLookup = await _clinicService.LookupByPhonesAsync(
            adminAccess,
            new LookupClinicsByPhonesDto { PhoneNumbers = request.ClinicPhoneNumbers },
            cancellationToken);

        if (!clinicLookup.Succeeded)
        {
            return AuthResult<RegisterResponse>.Failure(clinicLookup.Errors);
        }

        if (clinicLookup.Value!.Status == ClinicPhoneLookupStatus.Conflict)
        {
            return AuthResult<RegisterResponse>.Failure([ClinicErrors.ConflictingClinicPhones]);
        }

        var creatingNewClinic = clinicLookup.Value.Status == ClinicPhoneLookupStatus.None;
        if (creatingNewClinic)
        {
            if (string.IsNullOrWhiteSpace(request.NewClinicName)
                || string.IsNullOrWhiteSpace(request.NewClinicAddress))
            {
                return AuthResult<RegisterResponse>.Failure(
                    [ClinicErrors.ClinicCreateDetailsRequired]);
            }

            if (!request.ManagerIsThisDoctor)
            {
                return AuthResult<RegisterResponse>.Failure([ClinicErrors.ManagerIdRequired]);
            }
        }

        var profileAddress = ResolveClinicAddressForProfile(request, clinicLookup.Value);
        if (string.IsNullOrWhiteSpace(profileAddress))
        {
            return AuthResult<RegisterResponse>.Failure(
                [ClinicErrors.ClinicCreateDetailsRequired]);
        }

        // Doctors stay disabled until an administrator approves them.
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Role = UserRole.Doctor,
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow
        };

        UserCredentials.Apply(user, request.PhoneNumber, email: null);

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return AuthResult<RegisterResponse>.Failure(
                IdentityErrorLocalizer.Localize(createResult.Errors));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, nameof(UserRole.Doctor));
        if (!addRoleResult.Succeeded)
        {
            await DeleteRegisteredDoctorAsync(user, cancellationToken);
            return AuthResult<RegisterResponse>.Failure(
                IdentityErrorLocalizer.Localize(addRoleResult.Errors));
        }

        var profile = new DoctorProfile
        {
            DoctorProfileId = Guid.NewGuid(),
            DoctorId = user.Id,
            Specialty = request.Specialty?.Trim() ?? string.Empty,
            MedicalLicenseNumber = request.MedicalLicenseNumber?.Trim() ?? string.Empty,
            ClinicAddress = profileAddress,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = false,
        };
        _dbContext.DoctorProfiles.Add(profile);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            var assignResult = await _clinicService.AssignDoctorAsync(
                adminAccess,
                new AssignDoctorToClinicDto
                {
                    DoctorId = user.Id,
                    PhoneNumbers = request.ClinicPhoneNumbers,
                    Name = request.NewClinicName,
                    Address = request.NewClinicAddress,
                    ManagerIsThisDoctor = request.ManagerIsThisDoctor,
                    AllowDisabledDoctor = true,
                },
                cancellationToken);

            if (!assignResult.Succeeded)
            {
                await DeleteRegisteredDoctorAsync(user, cancellationToken);
                return AuthResult<RegisterResponse>.Failure(assignResult.Errors);
            }
        }
        catch
        {
            await DeleteRegisteredDoctorAsync(user, cancellationToken);
            throw;
        }

        var adminIds = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.IsEnabled)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count > 0)
        {
            await _notifications.NotifyManyAsync(
                adminIds,
                NotificationType.DoctorPendingActivation,
                "New doctor registration",
                $"{user.Name} registered and is waiting for approval.",
                new { doctorId = user.Id, doctorName = user.Name },
                cancellationToken);
        }

        return AuthResult<RegisterResponse>.Success(
            new RegisterResponse(
                user.Id,
                user.PhoneNumber!,
                user.Name,
                user.Role,
                RegisterMessages.DoctorRegistered));
    }

    private static string ResolveClinicAddressForProfile(
        RegisterRequest request,
        ClinicPhoneLookupResultDto clinicLookup)
    {
        if (clinicLookup.Status == ClinicPhoneLookupStatus.Found
            && clinicLookup.Clinic is not null)
        {
            return clinicLookup.Clinic.Address;
        }

        return request.NewClinicAddress?.Trim() ?? string.Empty;
    }

    private async Task DeleteRegisteredDoctorAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        // Clinic assignment uses this same scoped context and may leave the user,
        // profile, and clinic entities connected in the change tracker. With the
        // required User-DoctorProfile relationship configured as Restrict,
        // removing the tracked profile can otherwise be interpreted as severing
        // a required relationship (a conceptual null) instead of an explicit
        // dependent delete.
        _dbContext.ChangeTracker.Clear();

        var clinicLinks = await _dbContext.ClinicDoctors
            .Where(link => link.DoctorId == user.Id)
            .ToListAsync(cancellationToken);

        if (clinicLinks.Count > 0)
        {
            _dbContext.ClinicDoctors.RemoveRange(clinicLinks);
        }

        var profile = await _dbContext.DoctorProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.DoctorId == user.Id, cancellationToken);

        if (profile is not null)
        {
            _dbContext.DoctorProfiles.Remove(profile);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // The detached user instance still carries the old navigation reference.
        // Clear it before UserManager reattaches the user for deletion; otherwise
        // EF traverses that stale profile and recreates the required-relationship
        // conceptual-null error.
        user.DoctorProfile = null;
        await _userManager.DeleteAsync(user);
    }

    private async Task<ApplicationUser?> FindUserByPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        var lookupValues = UserCredentials.GetPhoneLookupValues(phoneNumber);
        return await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => lookupValues.Contains(u.PhoneNumber!)
                    || lookupValues.Contains(u.UserName!),
                cancellationToken);
    }

    private async Task<string?> ValidateUniquePhoneAsync(
        string phoneNumber,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var lookupValues = UserCredentials.GetPhoneLookupValues(phoneNumber);
        var phoneInUse = await _userManager.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u => u.Id != excludeUserId
                    && (lookupValues.Contains(u.PhoneNumber!)
                        || lookupValues.Contains(u.UserName!)),
                cancellationToken);

        return phoneInUse ? AuthErrorMessages.DuplicatePhoneNumber : null;
    }

    private async Task EnsureRoleExistsAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            var createRoleResult = await _roleManager.CreateAsync(
                new ApplicationRole { Id = Guid.NewGuid(), Name = roleName });

            if (!createRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join(", ", createRoleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
