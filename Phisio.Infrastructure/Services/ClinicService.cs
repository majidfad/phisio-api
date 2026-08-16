using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class ClinicService : IClinicService
{
    private readonly AppDbContext _dbContext;

    public ClinicService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<IReadOnlyList<ClinicDto>>> GetAllAsync(
        ClinicAccessContext access,
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var clinics = await ApplyAccessFilter(_dbContext.Clinics, access)
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .Include(clinic => clinic.PhoneNumbers)
            .OrderBy(clinic => clinic.Name)
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<ClinicDto>>.Success(
            clinics.Select(MapToDto).ToList());
    }

    public async Task<AuthResult<ClinicDto>> GetByIdAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await ApplyAccessFilter(_dbContext.Clinics, access)
            .AsNoTracking()
            .Include(c => c.PhoneNumbers)
            .FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);

        if (clinic is null)
        {
            return AuthResult<ClinicDto>.Failure([ClinicErrors.NotFound]);
        }

        return AuthResult<ClinicDto>.Success(MapToDto(clinic));
    }

    public async Task<AuthResult<ClinicDto>> CreateAsync(
        ClinicAccessContext access,
        CreateClinicDto request,
        CancellationToken cancellationToken = default)
    {
        var phoneNumbersResult = await ValidatePhoneNumbersAsync(
            request.PhoneNumbers,
            excludedClinicId: null,
            cancellationToken);
        if (!phoneNumbersResult.Succeeded)
        {
            return AuthResult<ClinicDto>.Failure(phoneNumbersResult.Errors);
        }

        var managerIdResult = await ResolveManagerIdAsync(access, request, cancellationToken);
        if (!managerIdResult.Succeeded)
        {
            return AuthResult<ClinicDto>.Failure(managerIdResult.Errors);
        }

        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            ClinicManagerId = managerIdResult.Value,
            PhoneNumbers = CreatePhoneNumbers(phoneNumbersResult.Value!),
        };

        clinic.EnsureManagerDoctorMembership();

        _dbContext.Clinics.Add(clinic);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsClinicPhoneUniqueViolation(exception))
        {
            return AuthResult<ClinicDto>.Failure([ClinicErrors.PhoneNumberAlreadyExists]);
        }

        return AuthResult<ClinicDto>.Success(MapToDto(clinic));
    }

    public async Task<AuthResult<ClinicDto>> UpdateAsync(
        ClinicAccessContext access,
        Guid clinicId,
        UpdateClinicDto request,
        CancellationToken cancellationToken = default)
    {
        var clinic = await ApplyAccessFilter(_dbContext.Clinics, access)
            .Include(c => c.PhoneNumbers)
            .FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);

        if (clinic is null)
        {
            return AuthResult<ClinicDto>.Failure([ClinicErrors.NotFound]);
        }

        var phoneNumbersResult = await ValidatePhoneNumbersAsync(
            request.PhoneNumbers,
            clinicId,
            cancellationToken);
        if (!phoneNumbersResult.Succeeded)
        {
            return AuthResult<ClinicDto>.Failure(phoneNumbersResult.Errors);
        }

        clinic.Name = request.Name.Trim();
        clinic.Address = request.Address.Trim();
        ReplacePhoneNumbers(clinic, phoneNumbersResult.Value!);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsClinicPhoneUniqueViolation(exception))
        {
            return AuthResult<ClinicDto>.Failure([ClinicErrors.PhoneNumberAlreadyExists]);
        }

        return AuthResult<ClinicDto>.Success(MapToDto(clinic));
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await ApplyAccessFilter(_dbContext.Clinics, access)
            .FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);

        if (clinic is null)
        {
            return AuthResult<bool>.Failure([ClinicErrors.NotFound]);
        }

        clinic.SoftDelete();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>> GetDoctorsAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await FindAccessibleClinicAsync(access, clinicId, cancellationToken);
        if (clinic is null)
        {
            return AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>.Failure([ClinicErrors.NotFound]);
        }

        var doctorLinks = await _dbContext.ClinicDoctors
            .AsNoTracking()
            .Where(link => link.ClinicId == clinicId)
            .Select(link => link.DoctorId)
            .ToListAsync(cancellationToken);

        if (doctorLinks.Count == 0)
        {
            return AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>.Success([]);
        }

        var doctors = await _dbContext.Users
            .AsNoTracking()
            .Where(user => doctorLinks.Contains(user.Id))
            .OrderBy(user => user.Name)
            .ToListAsync(cancellationToken);

        var profiles = await _dbContext.DoctorProfiles
            .AsNoTracking()
            .Where(profile => doctorLinks.Contains(profile.DoctorId))
            .ToDictionaryAsync(profile => profile.DoctorId, cancellationToken);

        var members = doctors
            .Select(doctor => MapToDoctorMemberDto(
                doctor,
                profiles.GetValueOrDefault(doctor.Id),
                clinic.ClinicManagerId))
            .ToList();

        return AuthResult<IReadOnlyList<ClinicDoctorMemberDto>>.Success(members);
    }

    public async Task<AuthResult<ClinicDoctorMemberDto>> AddDoctorAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await FindAccessibleClinicAsync(access, clinicId, cancellationToken);
        if (clinic is null)
        {
            return AuthResult<ClinicDoctorMemberDto>.Failure([ClinicErrors.NotFound]);
        }

        var doctor = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == doctorId, cancellationToken);

        if (doctor is null || !doctor.IsEnabled)
        {
            return AuthResult<ClinicDoctorMemberDto>.Failure([ClinicErrors.DoctorNotFound]);
        }

        if (!doctor.Role.HasDoctorAccess())
        {
            return AuthResult<ClinicDoctorMemberDto>.Failure([ClinicErrors.DoctorCannotBeAssigned]);
        }

        var alreadyAssigned = await _dbContext.ClinicDoctors
            .AnyAsync(link => link.ClinicId == clinicId && link.DoctorId == doctorId, cancellationToken);

        if (alreadyAssigned)
        {
            return AuthResult<ClinicDoctorMemberDto>.Failure([ClinicErrors.DoctorAlreadyAssigned]);
        }

        _dbContext.ClinicDoctors.Add(new ClinicDoctor
        {
            ClinicId = clinicId,
            DoctorId = doctorId,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var profile = await _dbContext.DoctorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

        return AuthResult<ClinicDoctorMemberDto>.Success(
            MapToDoctorMemberDto(doctor, profile, clinic.ClinicManagerId));
    }

    public async Task<AuthResult<bool>> RemoveDoctorAsync(
        ClinicAccessContext access,
        Guid clinicId,
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var clinic = await FindAccessibleClinicAsync(access, clinicId, cancellationToken);
        if (clinic is null)
        {
            return AuthResult<bool>.Failure([ClinicErrors.NotFound]);
        }

        if (doctorId == clinic.ClinicManagerId)
        {
            return AuthResult<bool>.Failure([ClinicErrors.CannotRemoveClinicManager]);
        }

        var link = await _dbContext.ClinicDoctors
            .FirstOrDefaultAsync(item => item.ClinicId == clinicId && item.DoctorId == doctorId, cancellationToken);

        if (link is null)
        {
            return AuthResult<bool>.Failure([ClinicErrors.DoctorNotFound]);
        }

        _dbContext.ClinicDoctors.Remove(link);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    private async Task<Clinic?> FindAccessibleClinicAsync(
        ClinicAccessContext access,
        Guid clinicId,
        CancellationToken cancellationToken) =>
        await ApplyAccessFilter(_dbContext.Clinics, access)
            .FirstOrDefaultAsync(clinic => clinic.ClinicId == clinicId, cancellationToken);

    private static ClinicDoctorMemberDto MapToDoctorMemberDto(
        ApplicationUser doctor,
        DoctorProfile? profile,
        Guid clinicManagerId) =>
        new(
            doctor.Id,
            doctor.Name,
            doctor.PhoneNumber!,
            doctor.Role,
            profile?.Specialty ?? string.Empty,
            doctor.Id == clinicManagerId);

    private static IQueryable<Clinic> ApplyAccessFilter(IQueryable<Clinic> query, ClinicAccessContext access) =>
        access.IsAdmin
            ? query
            : query.Where(clinic => clinic.ClinicManagerId == access.UserId);

    private async Task<AuthResult<Guid>> ResolveManagerIdAsync(
        ClinicAccessContext access,
        CreateClinicDto request,
        CancellationToken cancellationToken)
    {
        if (access.IsAdmin)
        {
            if (request.ClinicManagerId is null || request.ClinicManagerId == Guid.Empty)
            {
                return AuthResult<Guid>.Failure([ClinicErrors.ManagerIdRequired]);
            }

            var validation = await EnsureClinicManagerRoleAsync(
                request.ClinicManagerId.Value,
                allowExistingClinicManager: false,
                cancellationToken);

            return validation.Succeeded
                ? AuthResult<Guid>.Success(request.ClinicManagerId.Value)
                : AuthResult<Guid>.Failure(validation.Errors);
        }

        var selfValidation = await EnsureClinicManagerRoleAsync(
            access.UserId,
            allowExistingClinicManager: true,
            cancellationToken);

        return selfValidation.Succeeded
            ? AuthResult<Guid>.Success(access.UserId)
            : AuthResult<Guid>.Failure(selfValidation.Errors);
    }

    private async Task<AuthResult<bool>> EnsureClinicManagerRoleAsync(
        Guid managerId,
        bool allowExistingClinicManager,
        CancellationToken cancellationToken)
    {
        var manager = _dbContext.Users.Local
            .FirstOrDefault(user => user.Id == managerId)
            ?? await _dbContext.Users
                .FirstOrDefaultAsync(user => user.Id == managerId, cancellationToken);

        if (manager is null)
        {
            return AuthResult<bool>.Failure([ClinicErrors.ManagerNotFound]);
        }

        if (allowExistingClinicManager && manager.Role == UserRole.ClinicManager)
        {
            return AuthResult<bool>.Success(true);
        }

        if (manager.Role != UserRole.Doctor)
        {
            return AuthResult<bool>.Failure([ClinicErrors.ManagerMustBeDoctor]);
        }

        var clinicManagerRoleId = await _dbContext.Roles
            .Where(role => role.Name == RoleNames.ClinicManager)
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (clinicManagerRoleId is null)
        {
            return AuthResult<bool>.Failure([ClinicErrors.ManagerRoleNotConfigured]);
        }

        var hasClinicManagerRole = await _dbContext.UserRoles
            .AnyAsync(
                userRole => userRole.UserId == manager.Id
                    && userRole.RoleId == clinicManagerRoleId.Value,
                cancellationToken);

        if (!hasClinicManagerRole)
        {
            _dbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = manager.Id,
                RoleId = clinicManagerRoleId.Value,
            });
        }

        return AuthResult<bool>.Success(true);
    }

    private async Task<AuthResult<IReadOnlyList<NormalizedClinicPhone>>> ValidatePhoneNumbersAsync(
        IEnumerable<string> phoneNumbers,
        Guid? excludedClinicId,
        CancellationToken cancellationToken)
    {
        var normalizedPhoneNumbers = phoneNumbers
            .Select(phoneNumber => new NormalizedClinicPhone(
                phoneNumber.Trim(),
                PhoneNumberNormalizer.Normalize(phoneNumber)))
            .Where(phoneNumber => phoneNumber.Normalized.Length > 0)
            .GroupBy(phoneNumber => phoneNumber.Normalized)
            .Select(group => group.First())
            .ToList();

        if (normalizedPhoneNumbers.Count == 0)
        {
            return AuthResult<IReadOnlyList<NormalizedClinicPhone>>.Failure(
                [ClinicErrors.PhoneNumberRequired]);
        }

        var normalizedValues = normalizedPhoneNumbers
            .Select(phoneNumber => phoneNumber.Normalized)
            .ToList();
        var phoneNumberExists = await _dbContext.ClinicPhoneNumbers
            .AsNoTracking()
            .AnyAsync(
                phoneNumber => normalizedValues.Contains(phoneNumber.NormalizedPhoneNumber)
                    && (excludedClinicId == null || phoneNumber.ClinicId != excludedClinicId),
                cancellationToken);

        return phoneNumberExists
            ? AuthResult<IReadOnlyList<NormalizedClinicPhone>>.Failure(
                [ClinicErrors.PhoneNumberAlreadyExists])
            : AuthResult<IReadOnlyList<NormalizedClinicPhone>>.Success(normalizedPhoneNumbers);
    }

    private static List<ClinicPhoneNumber> CreatePhoneNumbers(
        IEnumerable<NormalizedClinicPhone> phoneNumbers) =>
        phoneNumbers
            .Select(phoneNumber => new ClinicPhoneNumber
            {
                ClinicPhoneNumberId = Guid.NewGuid(),
                PhoneNumber = phoneNumber.Display,
                NormalizedPhoneNumber = phoneNumber.Normalized,
            })
            .ToList();

    private static void ReplacePhoneNumbers(
        Clinic clinic,
        IEnumerable<NormalizedClinicPhone> phoneNumbers)
    {
        clinic.PhoneNumbers.Clear();

        foreach (var phoneNumber in CreatePhoneNumbers(phoneNumbers))
        {
            phoneNumber.ClinicId = clinic.ClinicId;
            clinic.PhoneNumbers.Add(phoneNumber);
        }
    }

    private static bool IsClinicPhoneUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_clinic_phone_numbers_NormalizedPhoneNumber",
        };

    private sealed record NormalizedClinicPhone(string Display, string Normalized);

    private static ClinicDto MapToDto(Clinic clinic) =>
        new(
            clinic.ClinicId,
            clinic.Name,
            clinic.Address,
            clinic.ClinicManagerId,
            clinic.PhoneNumbers
                .Select(phoneNumber => phoneNumber.PhoneNumber)
                .ToList(),
            clinic.CreatedAt,
            clinic.IsEnabled);
}
