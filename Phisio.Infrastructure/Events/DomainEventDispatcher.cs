using Phisio.Application.Common;
using Phisio.Domain.Common;

namespace Phisio.Infrastructure.Events;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly CareDomainEventNotificationHandler _notificationHandler;

    public DomainEventDispatcher(CareDomainEventNotificationHandler notificationHandler)
    {
        _notificationHandler = notificationHandler;
    }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        DispatchAsync([domainEvent], cancellationToken);

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _notificationHandler.HandleAsync(domainEvent, cancellationToken);
        }
    }
}

/// <summary>
/// No-op dispatcher for tests that do not care about side effects.
/// </summary>
public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
{
    public static NoOpDomainEventDispatcher Instance { get; } = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
