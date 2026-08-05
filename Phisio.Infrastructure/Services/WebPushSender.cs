using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phisio.Application.Notifications;
using Phisio.Infrastructure.Persistence;
using Phisio.Infrastructure.Push;
using WebPush;

namespace Phisio.Infrastructure.Services;

public class WebPushSender : IWebPushSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _dbContext;
    private readonly VapidSettings _vapid;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(
        AppDbContext dbContext,
        IOptions<VapidSettings> vapidOptions,
        ILogger<WebPushSender> logger)
    {
        _dbContext = dbContext;
        _vapid = vapidOptions.Value;
        _logger = logger;
    }

    public string? PublicKey => _vapid.IsConfigured ? _vapid.PublicKey : null;

    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        if (!_vapid.IsConfigured)
        {
            return;
        }

        var subscriptions = await _dbContext.PushSubscriptions
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                title,
                body,
                data,
            },
            JsonOptions);

        var vapidDetails = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
        var client = new WebPushClient();
        var stale = new List<Domain.Entities.PushSubscription>();

        foreach (var subscription in subscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var pushSubscription = new PushSubscription(
                    subscription.Endpoint,
                    subscription.P256dh,
                    subscription.Auth);

                await client.SendNotificationAsync(pushSubscription, payload, vapidDetails);
            }
            catch (WebPushException ex) when (
                ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                stale.Add(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send web push to user {UserId} endpoint {Endpoint}",
                    userId,
                    subscription.Endpoint);
            }
        }

        if (stale.Count > 0)
        {
            _dbContext.PushSubscriptions.RemoveRange(stale);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
