namespace Phisio.Domain.Common;

/// <summary>
/// Base type for persisted domain entities. Primary key names vary by entity
/// (for example ExerciseId, UserExerciseId) and are defined on derived types.
/// </summary>
public abstract class BaseEntity : IAuditableEntity, ISoftDeletable
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsEnabled { get; set; } = true;
}
