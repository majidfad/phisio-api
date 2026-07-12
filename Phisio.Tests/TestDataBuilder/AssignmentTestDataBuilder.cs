using Phisio.Domain.Entities;

namespace Phisio.Tests.TestDataBuilder;

internal static class AssignmentBuilder
{
    public static UserExercise Create(
        Guid doctorId,
        Guid patientId,
        Guid exerciseId,
        bool isActive = true,
        Guid? id = null,
        DateTime? assignedAt = null,
        DateOnly? scheduledDate = null) =>
        new()
        {
            UserExerciseId = id ?? Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            ExerciseId = exerciseId,
            AssignedAt = assignedAt ?? DateTime.UtcNow,
            ScheduledDate = scheduledDate ?? DateOnly.FromDateTime(assignedAt ?? DateTime.UtcNow),
            CreatedAt = assignedAt ?? DateTime.UtcNow,
            IsActive = isActive,
            IsEnabled = true
        };
}
