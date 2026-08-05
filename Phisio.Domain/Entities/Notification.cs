using Phisio.Domain.Common;
using Phisio.Domain.Enums;

namespace Phisio.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON payload for deep links and client-side i18n (ids, names, counts).
    /// </summary>
    public string? Data { get; set; }

    public bool IsRead { get; set; }
}
