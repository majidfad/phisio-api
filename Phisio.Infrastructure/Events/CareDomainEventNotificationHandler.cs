using Phisio.Application.Common;
using Phisio.Application.Notifications;
using Phisio.Domain.Common;
using Phisio.Domain.Enums;
using Phisio.Domain.Events;

namespace Phisio.Infrastructure.Events;

/// <summary>
/// Maps care-related domain events to in-app / push notifications.
/// </summary>
public sealed class CareDomainEventNotificationHandler
{
    private readonly INotificationService _notifications;

    public CareDomainEventNotificationHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        switch (domainEvent)
        {
            case CareRelationshipApprovedEvent approved:
                await _notifications.NotifyAsync(
                    approved.PatientId,
                    NotificationType.LinkApproved,
                    approved.DoctorInitiated ? "Care link created" : "Link approved",
                    approved.DoctorInitiated
                        ? $"{approved.DoctorName} added you as a patient at {approved.ClinicName}."
                        : $"{approved.DoctorName} accepted your care request.",
                    new
                    {
                        doctorId = approved.DoctorId,
                        doctorName = approved.DoctorName,
                        clinicId = approved.ClinicId,
                        clinicName = approved.ClinicName,
                    },
                    cancellationToken);
                break;

            case CareRelationshipRejectedEvent rejected:
                await _notifications.NotifyAsync(
                    rejected.PatientId,
                    NotificationType.LinkRejected,
                    "Link declined",
                    $"{rejected.DoctorName} declined your care request.",
                    new { doctorId = rejected.DoctorId, doctorName = rejected.DoctorName },
                    cancellationToken);
                break;

            case CareRelationshipRemovedEvent removed:
                await _notifications.NotifyAsync(
                    removed.PatientId,
                    NotificationType.PatientRemoved,
                    "Care link ended",
                    $"{removed.DoctorName} removed you from their patient list.",
                    new { doctorId = removed.DoctorId, doctorName = removed.DoctorName },
                    cancellationToken);
                break;

            case ExercisesAssignedEvent assigned:
                await _notifications.NotifyAsync(
                    assigned.PatientId,
                    NotificationType.ExercisesAssigned,
                    "New exercises assigned",
                    assigned.AssignedCount == 1
                        ? $"{assigned.DoctorName} assigned 1 exercise for you."
                        : $"{assigned.DoctorName} assigned {assigned.AssignedCount} exercises for you.",
                    new
                    {
                        doctorId = assigned.DoctorId,
                        doctorName = assigned.DoctorName,
                        count = assigned.AssignedCount,
                    },
                    cancellationToken);
                break;

            case ExerciseProgramCreatedEvent programCreated:
                await _notifications.NotifyAsync(
                    programCreated.PatientId,
                    NotificationType.ProgramCreated,
                    "New exercise program",
                    $"{programCreated.DoctorName} created an exercise program for you.",
                    new
                    {
                        doctorId = programCreated.DoctorId,
                        doctorName = programCreated.DoctorName,
                        programId = programCreated.ProgramId,
                        count = programCreated.AssignedCount,
                    },
                    cancellationToken);
                break;

            case CareRelationshipRequestedEvent requested:
                await _notifications.NotifyAsync(
                    requested.DoctorId,
                    NotificationType.PatientLinkRequested,
                    "New patient request",
                    $"{requested.PatientName} requested to link with you at {requested.ClinicName}.",
                    new
                    {
                        patientId = requested.PatientId,
                        patientName = requested.PatientName,
                        clinicId = requested.ClinicId,
                        clinicName = requested.ClinicName,
                    },
                    cancellationToken);
                break;

            case DailyFeedbackSubmittedEvent feedback:
                await _notifications.NotifyAsync(
                    feedback.DoctorId,
                    NotificationType.DailyFeedbackReceived,
                    feedback.WasUpdated ? "Daily feedback updated" : "New daily feedback",
                    feedback.WasUpdated
                        ? $"{feedback.PatientName} updated today's feedback."
                        : $"{feedback.PatientName} submitted today's feedback.",
                    new { patientId = feedback.PatientId, patientName = feedback.PatientName },
                    cancellationToken);
                break;

            case ExercisesCompletedEvent completed:
                await _notifications.NotifyAsync(
                    completed.DoctorId,
                    NotificationType.ExercisesCompleted,
                    "Exercises completed",
                    completed.CompletedCount == 1
                        ? $"{completed.PatientName} completed 1 exercise."
                        : $"{completed.PatientName} completed {completed.CompletedCount} exercises.",
                    new
                    {
                        patientId = completed.PatientId,
                        patientName = completed.PatientName,
                        count = completed.CompletedCount,
                    },
                    cancellationToken);
                break;
        }
    }
}
