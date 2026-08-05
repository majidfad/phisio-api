namespace Phisio.Application.Notifications;

public sealed record NotificationDto(
    Guid NotificationId,
    string Type,
    string Title,
    string Body,
    string? Data,
    bool IsRead,
    DateTime CreatedAt);

public sealed record UnreadCountDto(int Count);
