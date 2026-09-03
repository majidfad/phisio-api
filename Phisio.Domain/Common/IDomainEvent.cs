namespace Phisio.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
