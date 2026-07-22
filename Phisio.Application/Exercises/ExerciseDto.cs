using Phisio.Domain.Enums;

namespace Phisio.Application.Exercises;

public sealed record ExerciseDto(
    Guid ExerciseId,
    string Title,
    string Description,
    string Instructions,
    string? VideoUrl,
    ExerciseMediaType MediaType,
    ExerciseBodyRegion BodyRegion,
    ExerciseEquipment Equipment,
    ExerciseDifficulty Difficulty,
    Guid? CreatedByDoctorId,
    bool IsClinicShared,
    DateTime CreatedAt,
    bool IsEnabled = true);
