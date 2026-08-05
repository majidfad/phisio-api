namespace Phisio.Domain.Entities;

public class PushSubscription
{
    public Guid PushSubscriptionId { get; set; }

    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string P256dh { get; set; } = string.Empty;

    public string Auth { get; set; } = string.Empty;

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
