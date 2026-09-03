using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Application.Relationships;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Events;
using Phisio.Infrastructure.Events;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Services.Care;

namespace Phisio.Infrastructure.Services;

public class PatientDailyFeedbackService : IPatientDailyFeedbackService
{
    private readonly AppDbContext _dbContext;
    private readonly ICareRelationshipService _careRelationships;
    private readonly IDomainEventDispatcher _domainEvents;

    public PatientDailyFeedbackService(
        AppDbContext dbContext,
        ICareRelationshipService? careRelationships = null,
        IDomainEventDispatcher? domainEvents = null)
    {
        _dbContext = dbContext;
        _domainEvents = domainEvents ?? NoOpDomainEventDispatcher.Instance;
        _careRelationships = careRelationships
            ?? new CareRelationshipService(dbContext, _domainEvents);
    }

    public async Task<AuthResult<SubmitDailyFeedbackResponse>> SubmitAsync(
        Guid patientId,
        SubmitDailyFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var careContext = await ResolveCareContextAsync(patientId, request, today, cancellationToken);

        if (careContext is null)
        {
            return AuthResult<SubmitDailyFeedbackResponse>.Failure([PatientDailyFeedbackErrors.DoctorNotFound]);
        }

        var context = CareContext.From(
            careContext.Value.DoctorId,
            patientId,
            careContext.Value.ClinicId);

        if (!await _careRelationships.HasActiveRelationshipAsync(
                context.DoctorId,
                context.PatientId,
                context.ClinicId,
                cancellationToken))
        {
            return AuthResult<SubmitDailyFeedbackResponse>.Failure([PatientDailyFeedbackErrors.DoctorNotFound]);
        }

        var normalizedComment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        var existingFeedback = await _dbContext.DailyPatientFeedbacks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                feedback =>
                    feedback.PatientId == patientId
                    && feedback.DoctorId == context.DoctorId
                    && feedback.ClinicId == context.ClinicId
                    && feedback.FeedbackDate == today,
                cancellationToken);

        if (existingFeedback is not null)
        {
            existingFeedback.UpdateScores(
                request.ImprovementScore,
                request.HardnessScore,
                normalizedComment);
            existingFeedback.IsEnabled = true;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await DispatchFeedbackSubmittedAsync(
                patientId,
                context.DoctorId,
                context.ClinicId,
                wasUpdated: true,
                cancellationToken);

            return AuthResult<SubmitDailyFeedbackResponse>.Success(
                new SubmitDailyFeedbackResponse(
                    existingFeedback.DailyPatientFeedbackId,
                    existingFeedback.PatientId,
                    existingFeedback.DoctorId,
                    existingFeedback.ClinicId,
                    existingFeedback.FeedbackDate,
                    existingFeedback.ImprovementScore,
                    existingFeedback.HardnessScore,
                    existingFeedback.Comment,
                    WasUpdated: true));
        }

        var feedback = DailyPatientFeedback.Submit(
            context,
            today,
            request.ImprovementScore,
            request.HardnessScore,
            normalizedComment);

        _dbContext.DailyPatientFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await DispatchFeedbackSubmittedAsync(
            patientId,
            context.DoctorId,
            context.ClinicId,
            wasUpdated: false,
            cancellationToken);

        return AuthResult<SubmitDailyFeedbackResponse>.Success(
            new SubmitDailyFeedbackResponse(
                feedback.DailyPatientFeedbackId,
                feedback.PatientId,
                feedback.DoctorId,
                feedback.ClinicId,
                feedback.FeedbackDate,
                feedback.ImprovementScore,
                feedback.HardnessScore,
                feedback.Comment,
                WasUpdated: false));
    }

    private async Task DispatchFeedbackSubmittedAsync(
        Guid patientId,
        Guid doctorId,
        Guid clinicId,
        bool wasUpdated,
        CancellationToken cancellationToken)
    {
        var patientName = await CareExerciseCatalog.GetUserNameAsync(_dbContext, patientId, cancellationToken);
        await _domainEvents.DispatchAsync(
            new DailyFeedbackSubmittedEvent(
                doctorId,
                patientId,
                clinicId,
                patientName,
                wasUpdated,
                DateTime.UtcNow),
            cancellationToken);
    }

    private async Task<(Guid DoctorId, Guid ClinicId)?> ResolveCareContextAsync(
        Guid patientId,
        SubmitDailyFeedbackRequest request,
        DateOnly feedbackDate,
        CancellationToken cancellationToken)
    {
        if (request.DoctorId is { } requestedDoctorId && requestedDoctorId != Guid.Empty)
        {
            if (request.ClinicId is { } requestedClinicId && requestedClinicId != Guid.Empty)
            {
                return (requestedDoctorId, requestedClinicId);
            }

            var clinicIds = await _dbContext.DoctorPatients
                .AsNoTracking()
                .WhereActive()
                .Where(relationship =>
                    relationship.PatientId == patientId && relationship.DoctorId == requestedDoctorId)
                .Select(relationship => relationship.ClinicId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (clinicIds.Count == 1)
            {
                return (requestedDoctorId, clinicIds[0]);
            }

            return null;
        }

        var fromCompletion = await (
            from completion in _dbContext.ExerciseCompletions.AsNoTracking()
            join assignment in _dbContext.UserExercises.AsNoTracking()
                on completion.UserExerciseId equals assignment.UserExerciseId
            where completion.PatientId == patientId
                && completion.CompletionDate == feedbackDate
                && completion.IsEnabled
            orderby completion.CreatedAt descending
            select new { completion.DoctorId, assignment.ClinicId })
            .FirstOrDefaultAsync(cancellationToken);

        if (fromCompletion is not null)
        {
            return (fromCompletion.DoctorId, fromCompletion.ClinicId);
        }

        var fromRelationship = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(relationship => relationship.PatientId == patientId)
            .OrderByDescending(relationship => relationship.CreatedAt)
            .Select(relationship => new { relationship.DoctorId, relationship.ClinicId })
            .FirstOrDefaultAsync(cancellationToken);

        return fromRelationship is null
            ? null
            : (fromRelationship.DoctorId, fromRelationship.ClinicId);
    }
}
