using Phisio.Application.Common;
using Phisio.Domain.Enums;

namespace Phisio.Application.Notifications;

public interface INotificationService
{
    Task CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task CreateManyAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default);

    Task NotifyAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default);

    Task NotifyManyAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult<IReadOnlyList<NotificationDto>>> GetForUserAsync(
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<AuthResult<UnreadCountDto>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<bool>> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task<AuthResult<int>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
