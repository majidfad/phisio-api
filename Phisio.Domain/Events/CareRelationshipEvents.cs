using Phisio.Domain.Common;

namespace Phisio.Domain.Events;

public sealed record CareRelationshipApprovedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    string DoctorName,
    string ClinicName,
    bool DoctorInitiated,
    DateTime OccurredAt) : IDomainEvent;

public sealed record CareRelationshipRejectedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    string DoctorName,
    DateTime OccurredAt) : IDomainEvent;

public sealed record CareRelationshipRemovedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    string DoctorName,
    DateTime OccurredAt) : IDomainEvent;

public sealed record ExercisesAssignedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    string DoctorName,
    int AssignedCount,
    DateTime OccurredAt) : IDomainEvent;

public sealed record ExerciseProgramCreatedEvent(
    Guid DoctorId,
    Guid PatientId,
    Guid ClinicId,
    Guid ProgramId,
    string DoctorName,
    int AssignedCount,
    DateTime OccurredAt) : IDomainEvent;
