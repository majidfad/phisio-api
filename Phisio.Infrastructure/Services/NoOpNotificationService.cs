using Phisio.Application.Common;
using Phisio.Application.Notifications;
using Phisio.Domain.Enums;

namespace Phisio.Infrastructure.Services;

/// <summary>
/// No-op implementation so unit tests can construct services without wiring notifications.
/// </summary>
public sealed class NoOpNotificationService : INotificationService
{
    public static NoOpNotificationService Instance { get; } = new();

    public Task CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CreateManyAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyManyAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<AuthResult<IReadOnlyList<NotificationDto>>> GetForUserAsync(
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<IReadOnlyList<NotificationDto>>.Success([]));

    public Task<AuthResult<UnreadCountDto>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<UnreadCountDto>.Success(new UnreadCountDto(0)));

    public Task<AuthResult<bool>> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<bool>.Success(true));

    public Task<AuthResult<int>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthResult<int>.Success(0));
}
