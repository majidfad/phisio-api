using Phisio.Domain.Enums;

namespace Phisio.Application.Notifications;

public sealed record CreateNotificationRequest(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Body,
    string? Data = null);
