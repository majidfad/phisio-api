using Microsoft.EntityFrameworkCore;
using Phisio.Application.Notifications;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class PushSubscriptionService : IPushSubscriptionService
{
    private readonly AppDbContext _dbContext;

    public PushSubscriptionService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(
        Guid userId,
        PushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var endpoint = request.Endpoint.Trim();
        var existing = await _dbContext.PushSubscriptions
            .FirstOrDefaultAsync(p => p.Endpoint == endpoint, cancellationToken);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            _dbContext.PushSubscriptions.Add(new PushSubscription
            {
                PushSubscriptionId = Guid.NewGuid(),
                UserId = userId,
                Endpoint = endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserAgent = Truncate(userAgent, 500),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.UserAgent = Truncate(userAgent, 500);
            existing.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        Guid userId,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.PushSubscriptions
            .Where(p => p.UserId == userId && p.Endpoint == endpoint)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return;
        }

        _dbContext.PushSubscriptions.RemoveRange(items);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= max ? value : value[..max];
}
