using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.DoctorDashboard;
using Phisio.Application.ReadModels;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services.ReadModels;

/// <summary>
/// Read-only projections for the doctor dashboard. No aggregate mutations or business rules.
/// </summary>
public sealed class DoctorDashboardReadService : IDoctorDashboardReadService
{
    private readonly AppDbContext _dbContext;

    public DoctorDashboardReadService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<DoctorDashboardDto>> GetDashboardAsync(
        Guid doctorId,
        Guid? clinicId = null,
        CancellationToken cancellationToken = default)
    {
        var relationshipQuery = _dbContext.DoctorPatients
            .AsNoTracking()
            .Where(dp => dp.DoctorId == doctorId && dp.IsEnabled)
            .WhereClinic(clinicId);

        var patientsCount = await relationshipQuery
            .WhereActive()
            .CountAsync(cancellationToken);

        var pendingRequestsCount = await relationshipQuery
            .WherePending()
            .CountAsync(cancellationToken);

        var linkedPatientIds = await relationshipQuery
            .WhereActive()
            .Select(dp => dp.PatientId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignedExercisesCount = 0;
        var completedExercisesCount = 0;
        var feedbackCount = 0;

        if (clinicId is not null)
        {
            assignedExercisesCount = await _dbContext.UserExercises
                .AsNoTracking()
                .CountAsync(
                    ue => ue.DoctorId == doctorId
                        && ue.ClinicId == clinicId.Value
                        && ue.IsActive
                        && ue.IsEnabled,
                    cancellationToken);

            completedExercisesCount = await (
                from completion in _dbContext.ExerciseCompletions.AsNoTracking()
                join assignment in _dbContext.UserExercises.AsNoTracking()
                    on completion.UserExerciseId equals assignment.UserExerciseId
                where completion.DoctorId == doctorId
                    && completion.IsEnabled
                    && assignment.ClinicId == clinicId.Value
                select completion.ExerciseCompletionId)
                .CountAsync(cancellationToken);

            feedbackCount = await _dbContext.DailyPatientFeedbacks
                .AsNoTracking()
                .CountAsync(
                    feedback => feedback.DoctorId == doctorId
                        && feedback.ClinicId == clinicId.Value
                        && feedback.IsEnabled,
                    cancellationToken);
        }
        else if (linkedPatientIds.Count > 0)
        {
            assignedExercisesCount = await _dbContext.UserExercises
                .AsNoTracking()
                .CountAsync(
                    ue => ue.DoctorId == doctorId
                        && linkedPatientIds.Contains(ue.PatientId)
                        && ue.IsActive
                        && ue.IsEnabled,
                    cancellationToken);

            completedExercisesCount = await _dbContext.ExerciseCompletions
                .AsNoTracking()
                .CountAsync(
                    completion => completion.DoctorId == doctorId
                        && linkedPatientIds.Contains(completion.PatientId)
                        && completion.IsEnabled,
                    cancellationToken);

            feedbackCount = await _dbContext.DailyPatientFeedbacks
                .AsNoTracking()
                .CountAsync(
                    feedback => feedback.DoctorId == doctorId
                        && linkedPatientIds.Contains(feedback.PatientId)
                        && feedback.IsEnabled,
                    cancellationToken);
        }

        var recentPatients = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(dp => dp.DoctorId == doctorId)
            .WhereClinic(clinicId)
            .OrderByDescending(dp => dp.CreatedAt)
            .Take(5)
            .Join(
                _dbContext.Users.AsNoTracking().Where(u => u.Role == UserRole.Patient),
                dp => dp.PatientId,
                u => u.Id,
                (dp, u) => new { Relation = dp, Patient = u })
            .Join(
                _dbContext.Clinics.AsNoTracking().Where(clinic => clinic.IsEnabled),
                item => item.Relation.ClinicId,
                clinic => clinic.ClinicId,
                (item, clinic) => new DoctorDashboardRecentPatientDto(
                    item.Patient.Id,
                    item.Patient.Name,
                    item.Patient.PhoneNumber ?? string.Empty,
                    clinic.ClinicId,
                    clinic.Name))
            .ToListAsync(cancellationToken);

        return AuthResult<DoctorDashboardDto>.Success(
            new DoctorDashboardDto(
                patientsCount,
                pendingRequestsCount,
                assignedExercisesCount,
                completedExercisesCount,
                feedbackCount,
                recentPatients));
    }
}
