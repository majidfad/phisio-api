using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.PatientVisits;
using Phisio.Application.Relationships;
using Phisio.Domain.Enums;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PatientVisitService : IPatientVisitService
{
    private readonly AppDbContext _dbContext;
    private readonly ICareRelationshipService _careRelationships;

    public PatientVisitService(
        AppDbContext dbContext,
        ICareRelationshipService careRelationships)
    {
        _dbContext = dbContext;
        _careRelationships = careRelationships;
    }

    public async Task<AuthResult<PatientVisitDto>> RegisterVisitAsync(
        PatientVisitAccessContext access,
        RegisterPatientVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        var patient = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == request.PatientId && u.Role == UserRole.Patient && u.IsEnabled,
                cancellationToken);
        if (patient is null)
        {
            return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.PatientNotFound]);
        }

        var doctor = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == request.DoctorId && u.Role == UserRole.Doctor && u.IsEnabled,
                cancellationToken);
        if (doctor is null)
        {
            return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.DoctorNotFound]);
        }

        var clinic = await _dbContext.Clinics
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.ClinicId == request.ClinicId && c.IsEnabled,
                cancellationToken);
        if (clinic is null)
        {
            return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.ClinicNotFound]);
        }

        if (access.IsDoctor && request.DoctorId != access.UserId)
        {
            return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.DoctorMismatch]);
        }

        if (access.IsClinicManager && clinic.ClinicManagerId != access.UserId)
        {
            return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.ClinicManagerNotAuthorized]);
        }

        // Keep data consistent with existing doctor-patient-clinic rules.
        var isConnected = await _careRelationships.HasActiveRelationshipAsync(
            request.DoctorId,
            request.PatientId,
            request.ClinicId,
            cancellationToken);
        if (!isConnected)
        {
            return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.PatientNotConnectedToDoctor]);
        }

        // For clinic managers, ensure doctor belongs to the clinic (belt & suspenders).
        if (access.IsClinicManager)
        {
            var isDoctorInClinic = await _dbContext.ClinicDoctors
                .AsNoTracking()
                .AnyAsync(
                    cd => cd.ClinicId == request.ClinicId && cd.DoctorId == request.DoctorId,
                    cancellationToken);
            if (!isDoctorInClinic)
            {
                return AuthResult<PatientVisitDto>.Failure([PatientVisitErrors.DoctorNotFound]);
            }
        }

        var visit = new PatientVisit
        {
            PatientVisitId = Guid.NewGuid(),
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ClinicId = request.ClinicId,
            VisitAt = request.VisitAt,
            VisitType = request.VisitType,
            PatientCondition = request.PatientCondition,
            DoctorNotes = request.DoctorNotes,
            IsEnabled = true,
        };

        _dbContext.PatientVisits.Add(visit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<PatientVisitDto>.Success(
            new PatientVisitDto(
                visit.PatientVisitId,
                patient.Id,
                patient.Name,
                doctor.Id,
                doctor.Name,
                clinic.ClinicId,
                clinic.Name,
                visit.VisitAt,
                visit.VisitType,
                visit.PatientCondition,
                visit.DoctorNotes));
    }

    public Task<AuthResult<PatientVisitHistoryResponse>> GetPatientVisitsAsync(
        PatientVisitAccessContext access,
        Guid patientId,
        Guid? clinicId,
        Guid? doctorId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
        => GetVisitsInternalAsync(
            access: access,
            patientId: patientId,
            clinicId: clinicId,
            doctorId: doctorId,
            page: page,
            pageSize: pageSize,
            search: search,
            cancellationToken: cancellationToken,
            queryScope: VisitQueryScope.Patient);

    public Task<AuthResult<PatientVisitDto?>> GetMostRecentPatientVisitAsync(
        PatientVisitAccessContext access,
        Guid patientId,
        Guid? clinicId,
        Guid? doctorId,
        CancellationToken cancellationToken = default)
        => GetRecentVisitInternalAsync(
            access: access,
            patientId: patientId,
            clinicId: clinicId,
            doctorId: doctorId,
            cancellationToken: cancellationToken);

    public Task<AuthResult<PatientVisitHistoryResponse>> GetClinicVisitsAsync(
        PatientVisitAccessContext access,
        Guid clinicId,
        Guid? doctorId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
        => GetVisitsInternalAsync(
            access: access,
            patientId: null,
            clinicId: clinicId,
            doctorId: doctorId,
            page: page,
            pageSize: pageSize,
            search: search,
            cancellationToken: cancellationToken,
            queryScope: VisitQueryScope.Clinic);

    public Task<AuthResult<PatientVisitHistoryResponse>> GetDoctorVisitsAsync(
        PatientVisitAccessContext access,
        Guid doctorId,
        Guid? clinicId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
        => GetVisitsInternalAsync(
            access: access,
            patientId: null,
            clinicId: clinicId,
            doctorId: doctorId,
            page: page,
            pageSize: pageSize,
            search: search,
            cancellationToken: cancellationToken,
            queryScope: VisitQueryScope.Doctor);

    private enum VisitQueryScope
    {
        Patient,
        Clinic,
        Doctor,
    }

    private async Task<AuthResult<PatientVisitHistoryResponse>> GetVisitsInternalAsync(
        PatientVisitAccessContext access,
        Guid? patientId,
        Guid? clinicId,
        Guid? doctorId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken,
        VisitQueryScope queryScope)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        if (queryScope == VisitQueryScope.Patient && patientId is null)
        {
            return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.PatientNotFound]);
        }

        if (queryScope != VisitQueryScope.Patient && patientId is not null)
        {
            // Not expected: only set patientId for Patient scope.
            patientId = null;
        }

        if (access.IsPatient)
        {
            if (patientId is null || patientId != access.UserId)
            {
                return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.PatientNotFound]);
            }
        }

        // Doctor can only see their own visits (filtered by active doctor-patient relationship at creation-time).
        if (access.IsDoctor)
        {
            if (queryScope == VisitQueryScope.Doctor && doctorId != access.UserId)
            {
                return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.DoctorMismatch]);
            }

            if (doctorId is null)
            {
                doctorId = access.UserId;
            }
            else if (doctorId != access.UserId)
            {
                return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.DoctorMismatch]);
            }
        }

        // Clinic manager visibility is limited to their managed clinics.
        if (access.IsClinicManager)
        {
            if (clinicId is not null)
            {
                var clinic = await _dbContext.Clinics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        c => c.ClinicId == clinicId.Value && c.IsEnabled,
                        cancellationToken);
                if (clinic is null)
                {
                    return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.ClinicNotFound]);
                }

                if (clinic.ClinicManagerId != access.UserId)
                {
                    return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.ClinicManagerNotAuthorized]);
                }
            }
        }

        // Extra access gate: if doctor is requesting a specific patient, ensure they are connected.
        if (queryScope == VisitQueryScope.Patient && access.IsDoctor && patientId is not null)
        {
            var clinicGate = clinicId;
            var isConnected = await _careRelationships.HasActiveRelationshipAsync(
                access.UserId,
                patientId.Value,
                clinicGate,
                cancellationToken);
            if (!isConnected)
            {
                return AuthResult<PatientVisitHistoryResponse>.Failure([PatientVisitErrors.PatientNotConnectedToDoctor]);
            }
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

        var query =
            from visit in _dbContext.PatientVisits.AsNoTracking()
            join patient in _dbContext.Users.AsNoTracking() on visit.PatientId equals patient.Id
            join doctor in _dbContext.Users.AsNoTracking() on visit.DoctorId equals doctor.Id
            join clinic in _dbContext.Clinics.AsNoTracking() on visit.ClinicId equals clinic.ClinicId
            select new { visit, patient, doctor, clinic };

        if (patientId is Guid pid)
        {
            query = query.Where(x => x.visit.PatientId == pid);
        }

        if (clinicId is Guid cid)
        {
            query = query.Where(x => x.visit.ClinicId == cid);
        }

        if (doctorId is Guid did)
        {
            query = query.Where(x => x.visit.DoctorId == did);
        }

        if (access.IsClinicManager)
        {
            query = access.IsAdmin
                ? query
                : query.Where(x => x.clinic.ClinicManagerId == access.UserId);
        }

        if (normalizedSearch is not null)
        {
            query = query.Where(x =>
                x.patient.Name.ToLower().Contains(normalizedSearch)
                || x.doctor.Name.ToLower().Contains(normalizedSearch)
                || x.clinic.Name.ToLower().Contains(normalizedSearch)
                || (x.visit.DoctorNotes ?? string.Empty).ToLower().Contains(normalizedSearch));
        }

        query = query
            .OrderByDescending(x => x.visit.VisitAt)
            .ThenByDescending(x => x.visit.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var visits = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PatientVisitDto(
                x.visit.PatientVisitId,
                x.visit.PatientId,
                x.patient.Name,
                x.visit.DoctorId,
                x.doctor.Name,
                x.visit.ClinicId,
                x.clinic.Name,
                x.visit.VisitAt,
                x.visit.VisitType,
                x.visit.PatientCondition,
                x.visit.DoctorNotes))
            .ToListAsync(cancellationToken);

        return AuthResult<PatientVisitHistoryResponse>.Success(
            new PatientVisitHistoryResponse(visits, total, page, pageSize));
    }

    private async Task<AuthResult<PatientVisitDto?>> GetRecentVisitInternalAsync(
        PatientVisitAccessContext access,
        Guid patientId,
        Guid? clinicId,
        Guid? doctorId,
        CancellationToken cancellationToken)
    {
        if (access.IsPatient && patientId != access.UserId)
        {
            return AuthResult<PatientVisitDto?>.Failure([PatientVisitErrors.PatientNotFound]);
        }

        if (access.IsDoctor && doctorId is Guid did && did != access.UserId)
        {
            return AuthResult<PatientVisitDto?>.Failure([PatientVisitErrors.DoctorMismatch]);
        }

        if (access.IsDoctor && doctorId is null)
        {
            doctorId = access.UserId;
        }

        if (access.IsClinicManager && clinicId is not null)
        {
            var clinic = await _dbContext.Clinics
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.ClinicId == clinicId.Value && c.IsEnabled,
                    cancellationToken);
            if (clinic is null)
            {
                return AuthResult<PatientVisitDto?>.Failure([PatientVisitErrors.ClinicNotFound]);
            }

            if (clinic.ClinicManagerId != access.UserId)
            {
                return AuthResult<PatientVisitDto?>.Failure([PatientVisitErrors.ClinicManagerNotAuthorized]);
            }
        }

        // Keep consistent with creation rules.
        if (access.IsDoctor)
        {
            var isConnected = await _careRelationships.HasActiveRelationshipAsync(
                access.UserId,
                patientId,
                clinicId,
                cancellationToken);
            if (!isConnected)
            {
                return AuthResult<PatientVisitDto?>.Failure([PatientVisitErrors.PatientNotConnectedToDoctor]);
            }
        }

        var query =
            from visit in _dbContext.PatientVisits.AsNoTracking()
            join patient in _dbContext.Users.AsNoTracking() on visit.PatientId equals patient.Id
            join doctor in _dbContext.Users.AsNoTracking() on visit.DoctorId equals doctor.Id
            join clinic in _dbContext.Clinics.AsNoTracking() on visit.ClinicId equals clinic.ClinicId
            where visit.PatientId == patientId
            select new { visit, patient, doctor, clinic };

        if (clinicId is not null)
        {
            query = query.Where(x => x.visit.ClinicId == clinicId.Value);
        }

        if (doctorId is not null)
        {
            query = query.Where(x => x.visit.DoctorId == doctorId.Value);
        }

        if (access.IsClinicManager && !access.IsAdmin)
        {
            query = query.Where(x => x.clinic.ClinicManagerId == access.UserId);
        }

        var recent = await query
            .OrderByDescending(x => x.visit.VisitAt)
            .ThenByDescending(x => x.visit.CreatedAt)
            .Select(x => new PatientVisitDto(
                x.visit.PatientVisitId,
                x.visit.PatientId,
                x.patient.Name,
                x.visit.DoctorId,
                x.doctor.Name,
                x.visit.ClinicId,
                x.clinic.Name,
                x.visit.VisitAt,
                x.visit.VisitType,
                x.visit.PatientCondition,
                x.visit.DoctorNotes))
            .FirstOrDefaultAsync(cancellationToken);

        return AuthResult<PatientVisitDto?>.Success(recent);
    }
}

