using Phisio.Domain.Common;

namespace Phisio.Domain.Events;

public sealed record CareRelationshipRequestedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    string PatientName,
    string ClinicName,
    DateTime OccurredAt) : IDomainEvent;

public sealed record DailyFeedbackSubmittedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    string PatientName,
    bool WasUpdated,
    DateTime OccurredAt) : IDomainEvent;

public sealed record ExercisesCompletedEvent(
    Guid DoctorId,
    Guid PatientId,
    string PatientName,
    int CompletedCount,
    DateTime OccurredAt) : IDomainEvent;
