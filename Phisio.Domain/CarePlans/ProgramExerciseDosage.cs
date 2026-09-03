namespace Phisio.Domain.CarePlans;

/// <summary>Dosage details for a line item in an exercise program template.</summary>
public readonly record struct ProgramExerciseDosage(
    Guid ExerciseId,
    int? Sets,
    string? Reps,
    string? ClinicianNote,
    string? PatientCue);
