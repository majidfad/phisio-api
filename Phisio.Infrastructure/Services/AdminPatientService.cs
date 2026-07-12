using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Admin.Patients;
using Phisio.Application.Common;
using Phisio.Application.Patients;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class AdminPatientService : IAdminPatientService
{
    private const string PatientRoleName = nameof(UserRole.Patient);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AdminPatientService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<AuthResult<IReadOnlyList<PatientDto>>> GetAllAsync(
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        var patients = await _userManager.Users
            .AsNoTracking()
            .WhereEnabledStatus(isEnabled)
            .Where(user => user.Role == UserRole.Patient)
            .OrderBy(user => user.Name)
            .ToListAsync(cancellationToken);

        if (patients.Count == 0)
        {
            return AuthResult<IReadOnlyList<PatientDto>>.Success([]);
        }

        var patientIds = patients.Select(patient => patient.Id).ToList();
        var firstAssignments = await GetFirstAssignedAtByPatientIdsAsync(patientIds, cancellationToken);
        var doctorNamesByPatient = await GetDoctorNamesByPatientIdsAsync(patientIds, cancellationToken);

        var result = patients
            .Select(patient => MapToDto(
                patient,
                firstAssignments.TryGetValue(patient.Id, out var firstAssignedAt)
                    ? firstAssignedAt
                    : null,
                doctorNamesByPatient.GetValueOrDefault(patient.Id, [])))
            .ToList();

        return AuthResult<IReadOnlyList<PatientDto>>.Success(result);
    }

    public async Task<AuthResult<PatientDto>> GetByIdAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await FindPatientAsync(patientId, cancellationToken);

        if (patient is null)
        {
            return AuthResult<PatientDto>.Failure(["Patient not found."]);
        }

        var firstAssignedAt = await GetFirstAssignedAtAsync(patientId, cancellationToken);
        var doctorNames = await GetDoctorNamesByPatientIdsAsync([patientId], cancellationToken);
        return AuthResult<PatientDto>.Success(
            MapToDto(
                patient,
                firstAssignedAt,
                doctorNames.GetValueOrDefault(patientId, [])));
    }

    public async Task<AuthResult<PatientDto>> CreateAsync(
        CreateAdminPatientDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePatientRoleExistsAsync(cancellationToken);

        var validationError = await ValidateUniqueCredentialsAsync(
            request.PhoneNumber,
            request.Email,
            excludeUserId: null,
            cancellationToken);

        if (validationError is not null)
        {
            return AuthResult<PatientDto>.Failure([validationError]);
        }

        var patient = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Role = UserRole.Patient,
            CreatedAt = DateTime.UtcNow,
        };

        UserCredentials.Apply(patient, request.PhoneNumber, request.Email);

        var createResult = await _userManager.CreateAsync(patient, TemporaryPasswordGenerator.Generate());

        if (!createResult.Succeeded)
        {
            return AuthResult<PatientDto>.Failure(
                createResult.Errors.Select(error => error.Description));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(patient, PatientRoleName);

        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(patient);
            return AuthResult<PatientDto>.Failure(
                addRoleResult.Errors.Select(error => error.Description));
        }

        return AuthResult<PatientDto>.Success(MapToDto(patient, firstAssignedAt: null, doctorNames: []));
    }

    public async Task<AuthResult<PatientDto>> UpdateAsync(
        Guid patientId,
        UpdateAdminPatientDto request,
        CancellationToken cancellationToken = default)
    {
        var patient = await _userManager.FindByIdAsync(patientId.ToString());

        if (patient is null || patient.Role != UserRole.Patient)
        {
            return AuthResult<PatientDto>.Failure(["Patient not found."]);
        }

        var validationError = await ValidateUniqueCredentialsAsync(
            request.PhoneNumber,
            request.Email,
            excludeUserId: patientId,
            cancellationToken);

        if (validationError is not null)
        {
            return AuthResult<PatientDto>.Failure([validationError]);
        }

        patient.Name = request.Name.Trim();
        UserCredentials.Apply(patient, request.PhoneNumber, request.Email);

        var updateResult = await _userManager.UpdateAsync(patient);

        if (!updateResult.Succeeded)
        {
            return AuthResult<PatientDto>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        var firstAssignedAt = await GetFirstAssignedAtAsync(patientId, cancellationToken);
        var doctorNames = await GetDoctorNamesByPatientIdsAsync([patientId], cancellationToken);
        return AuthResult<PatientDto>.Success(
            MapToDto(
                patient,
                firstAssignedAt,
                doctorNames.GetValueOrDefault(patientId, [])));
    }

    public async Task<AuthResult<bool>> DeleteAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await _userManager.Users
            .FirstOrDefaultAsync(
                user => user.Id == patientId && user.Role == UserRole.Patient,
                cancellationToken);

        if (patient is null)
        {
            return AuthResult<bool>.Failure(["Patient not found."]);
        }

        var assignments = await _dbContext.UserExercises
            .Where(assignment => assignment.PatientId == patientId)
            .ToListAsync(cancellationToken);

        SoftDeleteExtensions.SoftDeleteRange(assignments);

        patient.SoftDelete();

        var updateResult = await _userManager.UpdateAsync(patient);

        if (!updateResult.Succeeded)
        {
            return AuthResult<bool>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> ActivateAsync(
        Guid patientId,
        CancellationToken cancellationToken = default)
    {
        var patient = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                user => user.Id == patientId && user.Role == UserRole.Patient,
                cancellationToken);

        if (patient is null)
        {
            return AuthResult<bool>.Failure(["Patient not found."]);
        }

        if (patient.IsEnabled)
        {
            return AuthResult<bool>.Failure(["Patient is already active."]);
        }

        patient.IsEnabled = true;

        var updateResult = await _userManager.UpdateAsync(patient);

        if (!updateResult.Succeeded)
        {
            return AuthResult<bool>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    private async Task<ApplicationUser?> FindPatientAsync(
        Guid patientId,
        CancellationToken cancellationToken)
    {
        return await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == patientId && user.Role == UserRole.Patient,
                cancellationToken);
    }

    private async Task<DateTime?> GetFirstAssignedAtAsync(
        Guid patientId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserExercises
            .AsNoTracking()
            .Where(assignment => assignment.PatientId == patientId)
            .Select(assignment => (DateTime?)assignment.AssignedAt)
            .MinAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, DateTime>> GetFirstAssignedAtByPatientIdsAsync(
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken cancellationToken)
    {
        if (patientIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.UserExercises
            .AsNoTracking()
            .Where(assignment => patientIds.Contains(assignment.PatientId))
            .GroupBy(assignment => assignment.PatientId)
            .Select(group => new
            {
                PatientId = group.Key,
                FirstAssignedAt = group.Min(assignment => assignment.AssignedAt),
            })
            .ToDictionaryAsync(
                item => item.PatientId,
                item => item.FirstAssignedAt,
                cancellationToken);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetDoctorNamesByPatientIdsAsync(
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken cancellationToken)
    {
        if (patientIds.Count == 0)
        {
            return [];
        }

        var rows = await _dbContext.UserExercises
            .AsNoTracking()
            .Where(assignment => patientIds.Contains(assignment.PatientId))
            .Join(
                _dbContext.Users.AsNoTracking().Where(user => user.Role == UserRole.Doctor),
                assignment => assignment.DoctorId,
                doctor => doctor.Id,
                (assignment, doctor) => new { assignment.PatientId, doctor.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PatientId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList());
    }

    private static PatientDto MapToDto(
        ApplicationUser patient,
        DateTime? firstAssignedAt,
        IReadOnlyList<string> doctorNames) =>
        new(
            patient.Id,
            patient.Name,
            patient.PhoneNumber ?? string.Empty,
            firstAssignedAt ?? patient.CreatedAt,
            patient.Email,
            patient.CreatedAt,
            doctorNames,
            patient.IsEnabled);

    private async Task<string?> ValidateUniqueCredentialsAsync(
        string phoneNumber,
        string? email,
        Guid? excludeUserId,
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

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var existingByEmail = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                user => user.NormalizedEmail == email.Trim().ToUpperInvariant(),
                cancellationToken);

        if (existingByEmail is not null && existingByEmail.Id != excludeUserId)
        {
            return "Email is already registered.";
        }

        return null;
    }

    private async Task EnsurePatientRoleExistsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await _roleManager.RoleExistsAsync(PatientRoleName))
        {
            return;
        }

        var createRoleResult = await _roleManager.CreateAsync(
            new ApplicationRole { Id = Guid.NewGuid(), Name = PatientRoleName });

        if (!createRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create role '{PatientRoleName}': {string.Join(", ", createRoleResult.Errors.Select(error => error.Description))}");
        }
    }
}
