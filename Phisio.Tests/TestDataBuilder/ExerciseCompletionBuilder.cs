using Phisio.Domain.Entities;

namespace Phisio.Tests.TestDataBuilder;

internal static class ExerciseCompletionBuilder
{
    public static ExerciseCompletion Create(
        Guid userExerciseId,
        Guid patientId,
        Guid doctorId,
        Guid exerciseId,
        DateOnly? completionDate = null,
        bool isEnabled = true,
        Guid? id = null) =>
        new()
        {
            ExerciseCompletionId = id ?? Guid.NewGuid(),
            UserExerciseId = userExerciseId,
            PatientId = patientId,
            DoctorId = doctorId,
            ExerciseId = exerciseId,
            CompletionDate = completionDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
        };
}
