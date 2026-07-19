using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Auth;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Authentication;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenService = jwtTokenService;
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
            return user.Role == UserRole.Doctor
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

        return AuthResult<AuthResponse>.Success(
            new AuthResponse(
                tokenResult.AccessToken,
                tokenResult.ExpiresAt,
                user.Id,
                user.PhoneNumber!,
                user.Email,
                user.Name,
                user.Role));
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

        // Persisted together with the user by the Identity EF store.
        user.DoctorProfile = new DoctorProfile
        {
            DoctorProfileId = Guid.NewGuid(),
            DoctorId = user.Id,
            Specialty = request.Specialty?.Trim() ?? string.Empty,
            MedicalLicenseNumber = request.MedicalLicenseNumber?.Trim() ?? string.Empty,
            ClinicAddress = string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = false
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return AuthResult<RegisterResponse>.Failure(
                IdentityErrorLocalizer.Localize(createResult.Errors));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, nameof(UserRole.Doctor));
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
                RegisterMessages.DoctorRegistered));
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
