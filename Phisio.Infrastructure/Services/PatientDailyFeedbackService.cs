using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.PatientDailyFeedback;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PatientDailyFeedbackService : IPatientDailyFeedbackService
{
    private readonly AppDbContext _dbContext;

    public PatientDailyFeedbackService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthResult<SubmitDailyFeedbackResponse>> SubmitAsync(
        Guid patientId,
        SubmitDailyFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var doctorId = await ResolveDoctorIdAsync(patientId, today, cancellationToken);

        if (doctorId is null)
        {
            return AuthResult<SubmitDailyFeedbackResponse>.Failure([PatientDailyFeedbackErrors.DoctorNotFound]);
        }

        var normalizedComment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        var existingFeedback = await _dbContext.DailyPatientFeedbacks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                feedback => feedback.PatientId == patientId && feedback.FeedbackDate == today,
                cancellationToken);

        if (existingFeedback is not null)
        {
            existingFeedback.DoctorId = doctorId.Value;
            existingFeedback.ImprovementScore = request.ImprovementScore;
            existingFeedback.Comment = normalizedComment;
            existingFeedback.IsEnabled = true;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return AuthResult<SubmitDailyFeedbackResponse>.Success(
                new SubmitDailyFeedbackResponse(
                    existingFeedback.DailyPatientFeedbackId,
                    existingFeedback.PatientId,
                    existingFeedback.DoctorId,
                    existingFeedback.FeedbackDate,
                    existingFeedback.ImprovementScore,
                    existingFeedback.Comment,
                    WasUpdated: true));
        }

        var feedback = new DailyPatientFeedback
        {
            DailyPatientFeedbackId = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId.Value,
            FeedbackDate = today,
            ImprovementScore = request.ImprovementScore,
            Comment = normalizedComment,
            IsEnabled = true,
        };

        _dbContext.DailyPatientFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return AuthResult<SubmitDailyFeedbackResponse>.Success(
            new SubmitDailyFeedbackResponse(
                feedback.DailyPatientFeedbackId,
                feedback.PatientId,
                feedback.DoctorId,
                feedback.FeedbackDate,
                feedback.ImprovementScore,
                feedback.Comment,
                WasUpdated: false));
    }

    private async Task<Guid?> ResolveDoctorIdAsync(
        Guid patientId,
        DateOnly feedbackDate,
        CancellationToken cancellationToken)
    {
        var doctorIdFromCompletion = await _dbContext.ExerciseCompletions
            .AsNoTracking()
            .Where(completion =>
                completion.PatientId == patientId
                && completion.CompletionDate == feedbackDate
                && completion.IsEnabled)
            .OrderByDescending(completion => completion.CreatedAt)
            .Select(completion => completion.DoctorId)
            .FirstOrDefaultAsync(cancellationToken);

        if (doctorIdFromCompletion != Guid.Empty)
        {
            return doctorIdFromCompletion;
        }

        var doctorIdFromRelationship = await _dbContext.DoctorPatients
            .AsNoTracking()
            .WhereActive()
            .Where(relationship => relationship.PatientId == patientId)
            .OrderByDescending(relationship => relationship.CreatedAt)
            .Select(relationship => relationship.DoctorId)
            .FirstOrDefaultAsync(cancellationToken);

        return doctorIdFromRelationship == Guid.Empty ? null : doctorIdFromRelationship;
    }
}
