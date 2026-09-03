using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorPatients;
using Phisio.Application.Relationships;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Domain.Events;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public sealed class CareRelationshipService : ICareRelationshipService
{
    public const string PatientNotFoundError = DoctorPatientErrors.PatientNotFound;
    public const string RelationshipNotFoundError = DoctorPatientErrors.RelationshipNotFound;
    public const string RequestNotFoundError = DoctorPatientErrors.RequestNotFound;

    private readonly AppDbContext _dbContext;
    private readonly IDomainEventDispatcher _domainEvents;

    public CareRelationshipService(
        AppDbContext dbContext,
        IDomainEventDispatcher domainEvents)
    {
        _dbContext = dbContext;
        _domainEvents = domainEvents;
    }

    public async Task<AuthResult<IReadOnlyList<DoctorPatientDto>>> GetPatientsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default)
    {
        var patients = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(dp => dp.DoctorId == doctorId)
            .WhereClinic(clinicId)
            .Join(
                _dbContext.Users
                    .AsNoTracking()
                    .Where(u =>
                        u.Role == UserRole.Patient &&
                        u.IsEnabled),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new
                {
                    Patient = u,
                    Relation = dp,
                })
            .Join(
                _dbContext.Clinics.AsNoTracking().Where(c => c.IsEnabled),
                item => item.Relation.ClinicId,
                clinic => clinic.ClinicId,
                (item, clinic) => new
                {
                    item.Patient,
                    item.Relation,
                    Clinic = clinic,
                })
            .OrderBy(x => x.Patient.Name)
            .ThenBy(x => x.Clinic.Name)
            .Select(x => new DoctorPatientDto(
                x.Patient.Id,
                x.Patient.Name,
                x.Patient.PhoneNumber ?? string.Empty,
                x.Relation.CreatedAt,
                x.Clinic.ClinicId,
                x.Clinic.Name))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorPatientDto>>.Success(patients);
    }

    public async Task<AuthResult<IReadOnlyList<DoctorPatientRequestDto>>> GetPendingRequestsAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default)
    {
        var requests = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WherePending()
            .Where(dp => dp.DoctorId == doctorId)
            .WhereClinic(clinicId)
            .Join(
                _dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.Role == UserRole.Patient && u.IsEnabled),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new { Relation = dp, Patient = u })
            .Join(
                _dbContext.Clinics.AsNoTracking().Where(c => c.IsEnabled),
                item => item.Relation.ClinicId,
                clinic => clinic.ClinicId,
                (item, clinic) => new { item.Relation, item.Patient, Clinic = clinic })
            .OrderByDescending(x => x.Relation.CreatedAt)
            .Select(x => new DoctorPatientRequestDto(
                x.Patient.Id,
                x.Patient.Name,
                x.Patient.PhoneNumber ?? string.Empty,
                x.Relation.CreatedAt,
                x.Clinic.ClinicId,
                x.Clinic.Name))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<DoctorPatientRequestDto>>.Success(requests);
    }

    public async Task<AuthResult<IReadOnlyList<DoctorClinicOptionDto>>> GetMyClinicsAsync(
        Guid doctorId,
        CancellationToken cancellationToken = default)
    {
        var counts = await _dbContext.DoctorPatients
            .AsNoTracking()
            .Where(dp => dp.DoctorId == doctorId && dp.IsEnabled)
            .GroupBy(dp => new { dp.ClinicId, dp.Status })
            .Select(group => new
            {
                group.Key.ClinicId,
                group.Key.Status,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var clinics = await (
            from membership in _dbContext.ClinicDoctors.AsNoTracking()
            join clinic in _dbContext.Clinics.AsNoTracking()
                on membership.ClinicId equals clinic.ClinicId
            where membership.DoctorId == doctorId && clinic.IsEnabled
            orderby clinic.Name
            select new
            {
                clinic.ClinicId,
                clinic.Name,
                clinic.Address,
            })
            .ToListAsync(cancellationToken);

        var result = clinics
            .Select(clinic => new DoctorClinicOptionDto(
                clinic.ClinicId,
                clinic.Name,
                clinic.Address,
                counts
                    .Where(item =>
                        item.ClinicId == clinic.ClinicId && item.Status == DoctorPatientStatus.Approved)
                    .Sum(item => item.Count),
                counts
                    .Where(item =>
                        item.ClinicId == clinic.ClinicId && item.Status == DoctorPatientStatus.Pending)
                    .Sum(item => item.Count)))
            .ToList();

        return AuthResult<IReadOnlyList<DoctorClinicOptionDto>>.Success(result);
    }

    public async Task<AuthResult<DoctorPatientLookupDto>> LookupPatientByPhoneAsync(
        Guid doctorId,
        string? phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return AuthResult<DoctorPatientLookupDto>.Failure([DoctorPatientErrors.PhoneNumberRequired]);
        }

        var lookupValues = UserCredentials.GetPhoneLookupValues(phoneNumber);
        if (lookupValues.Count == 0)
        {
            return AuthResult<DoctorPatientLookupDto>.Failure([DoctorPatientErrors.PhoneNumberRequired]);
        }

        var patient = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Patient && user.IsEnabled)
            .Where(user =>
                lookupValues.Contains(user.PhoneNumber!) || lookupValues.Contains(user.UserName!))
            .Select(user => new DoctorPatientLookupDto(
                user.Id,
                user.Name,
                user.PhoneNumber ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
        {
            return AuthResult<DoctorPatientLookupDto>.Failure([DoctorPatientErrors.PatientPhoneNotFound]);
        }

        return AuthResult<DoctorPatientLookupDto>.Success(patient);
    }

    public async Task<AuthResult<DoctorPatientDto>> AddPatientAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var membership = await ValidateDoctorClinicMembershipAsync(doctorId, clinicId, cancellationToken);
        if (!membership.Succeeded)
        {
            return AuthResult<DoctorPatientDto>.Failure(membership.Errors);
        }

        var patient = await _dbContext.Users
            .FirstOrDefaultAsync(
                user => user.Id == patientId && user.Role == UserRole.Patient && user.IsEnabled,
                cancellationToken);

        if (patient is null)
        {
            return AuthResult<DoctorPatientDto>.Failure([PatientNotFoundError]);
        }

        var context = CareContext.From(doctorId, patientId, clinicId);
        var existing = await _dbContext.DoctorPatients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (existing is { IsEnabled: true, Status: DoctorPatientStatus.Approved })
        {
            return AuthResult<DoctorPatientDto>.Failure([DoctorPatientErrors.AlreadyLinked]);
        }

        var availability = await EnsurePatientCanOpenCareLinkAsync(
            patientId,
            doctorId,
            clinicId,
            cancellationToken);
        if (!availability.Succeeded)
        {
            return AuthResult<DoctorPatientDto>.Failure(availability.Errors);
        }

        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.ReestablishAsApproved(now);
        }
        else
        {
            _dbContext.DoctorPatients.Add(DoctorPatient.CreateApproved(context, now));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var clinic = membership.Value!;
        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _domainEvents.DispatchAsync(
            new CareRelationshipApprovedEvent(
                doctorId,
                patientId,
                clinicId,
                doctorName,
                clinic.Name,
                DoctorInitiated: true,
                now),
            cancellationToken);

        return AuthResult<DoctorPatientDto>.Success(new DoctorPatientDto(
            patient.Id,
            patient.Name,
            patient.PhoneNumber ?? string.Empty,
            now,
            clinic.ClinicId,
            clinic.Name));
    }

    public async Task<AuthResult<DoctorPatientDto>> ApproveRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<DoctorPatientDto>.Failure([RequestNotFoundError]);
        }

        var patient = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == patientId && u.Role == UserRole.Patient && u.IsEnabled,
                cancellationToken);

        if (patient is null)
        {
            return AuthResult<DoctorPatientDto>.Failure([PatientNotFoundError]);
        }

        var availability = await EnsurePatientCanOpenCareLinkAsync(
            patientId,
            doctorId,
            clinicId,
            cancellationToken);
        if (!availability.Succeeded)
        {
            return AuthResult<DoctorPatientDto>.Failure(availability.Errors);
        }

        var approvedAt = DateTime.UtcNow;
        relationship.Approve(approvedAt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        var clinicName = await _dbContext.Clinics
            .AsNoTracking()
            .Where(clinic => clinic.ClinicId == clinicId)
            .Select(clinic => clinic.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? string.Empty;

        await _domainEvents.DispatchAsync(
            new CareRelationshipApprovedEvent(
                doctorId,
                patientId,
                clinicId,
                doctorName,
                clinicName,
                DoctorInitiated: false,
                approvedAt),
            cancellationToken);

        return AuthResult<DoctorPatientDto>.Success(new DoctorPatientDto(
            patient.Id,
            patient.Name,
            patient.PhoneNumber ?? string.Empty,
            relationship.CreatedAt,
            clinicId,
            clinicName));
    }

    public async Task<AuthResult<bool>> RejectRequestAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WherePending()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([RequestNotFoundError]);
        }

        relationship.Reject();
        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _domainEvents.DispatchAsync(
            new CareRelationshipRejectedEvent(
                doctorId,
                patientId,
                clinicId,
                doctorName,
                DateTime.UtcNow),
            cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> RemoveAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _dbContext.DoctorPatients
            .WhereActive()
            .FirstOrDefaultAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId && dp.ClinicId == clinicId,
                cancellationToken);

        if (relationship is null)
        {
            return AuthResult<bool>.Failure([RelationshipNotFoundError]);
        }

        relationship.SoftRemove();
        await _dbContext.SaveChangesAsync(cancellationToken);

        var doctorName = await GetUserNameAsync(doctorId, cancellationToken);
        await _domainEvents.DispatchAsync(
            new CareRelationshipRemovedEvent(
                doctorId,
                patientId,
                clinicId,
                doctorName,
                DateTime.UtcNow),
            cancellationToken);

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<bool>> EnsureCareAccessAsync(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        if (clinicId == Guid.Empty)
        {
            return AuthResult<bool>.Failure([DoctorPatientErrors.ClinicRequired]);
        }

        if (!await HasActiveRelationshipAsync(doctorId, patientId, clinicId, cancellationToken))
        {
            return AuthResult<bool>.Failure([PatientNotFoundError]);
        }

        return AuthResult<bool>.Success(true);
    }

    public Task<bool> HasActiveRelationshipAsync(
        Guid doctorId,
        Guid patientId,
        Guid? clinicId,
        CancellationToken cancellationToken = default) =>
        _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .WhereClinic(clinicId)
            .AnyAsync(
                dp => dp.DoctorId == doctorId && dp.PatientId == patientId,
                cancellationToken);

    public async Task<AuthResult<bool>> EnsurePatientCanOpenCareLinkAsync(
        Guid patientId,
        Guid doctorId,
        Guid clinicId,
        CancellationToken cancellationToken = default)
    {
        var hasOtherOpenCare = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereOpenCare()
            .AnyAsync(
                dp => dp.PatientId == patientId
                    && !(dp.DoctorId == doctorId && dp.ClinicId == clinicId),
                cancellationToken);

        if (hasOtherOpenCare)
        {
            return AuthResult<bool>.Failure([DoctorPatientErrors.PatientAlreadyLinkedElsewhere]);
        }

        return AuthResult<bool>.Success(true);
    }

    private async Task<AuthResult<Clinic>> ValidateDoctorClinicMembershipAsync(
        Guid doctorId,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ClinicId == clinicId && item.IsEnabled,
                cancellationToken);

        if (clinic is null)
        {
            return AuthResult<Clinic>.Failure([DoctorPatientErrors.ClinicNotFound]);
        }

        var isMember = await _dbContext.ClinicDoctors
            .AsNoTracking()
            .AnyAsync(
                membership => membership.ClinicId == clinicId && membership.DoctorId == doctorId,
                cancellationToken);

        if (!isMember)
        {
            return AuthResult<Clinic>.Failure([DoctorPatientErrors.DoctorNotInClinic]);
        }

        return AuthResult<Clinic>.Success(clinic);
    }

    private async Task<string> GetUserNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken)
        ?? "User";
}
