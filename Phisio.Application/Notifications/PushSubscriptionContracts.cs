namespace Phisio.Application.Notifications;

public sealed record PushSubscriptionRequest(
    string Endpoint,
    string P256dh,
    string Auth);

public sealed record VapidPublicKeyDto(string PublicKey);

public interface IPushSubscriptionService
{
    Task UpsertAsync(
        Guid userId,
        PushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid userId,
        string endpoint,
        CancellationToken cancellationToken = default);
}

public interface IWebPushSender
{
    string? PublicKey { get; }

    Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default);
}
