using Phisio.Domain.Common;

namespace Phisio.Domain.Entities;

public class ExerciseCategory : BaseEntity
{
    public Guid ExerciseCategoryId { get; set; }

    public string NameFa { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<ExerciseCategoryLink> ExerciseLinks { get; set; } = new List<ExerciseCategoryLink>();
}
