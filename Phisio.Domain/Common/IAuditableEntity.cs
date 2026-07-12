namespace Phisio.Domain.Common;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
}
