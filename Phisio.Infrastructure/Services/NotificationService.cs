using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Phisio.Application.Common;
using Phisio.Application.Notifications;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Persistence;

namespace Phisio.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _dbContext;
    private readonly IWebPushSender _webPushSender;

    public NotificationService(
        AppDbContext dbContext,
        IWebPushSender? webPushSender = null)
    {
        _dbContext = dbContext;
        _webPushSender = webPushSender ?? NullWebPushSender.Instance;
    }

    public async Task CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Notifications.Add(ToEntity(request));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PushAsync(request, cancellationToken);
    }

    public async Task CreateManyAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var list = requests.ToList();
        var entities = list.Select(ToEntity).ToList();
        if (entities.Count == 0)
        {
            return;
        }

        _dbContext.Notifications.AddRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var request in list)
        {
            await PushAsync(request, cancellationToken);
        }
    }

    public Task NotifyAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(
            new CreateNotificationRequest(
                userId,
                type,
                title,
                body,
                SerializeData(data)),
            cancellationToken);
    }

    public Task NotifyManyAsync(
        IEnumerable<Guid> userIds,
        NotificationType type,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        var dataJson = SerializeData(data);
        var requests = userIds
            .Distinct()
            .Select(userId => new CreateNotificationRequest(
                userId,
                type,
                title,
                body,
                dataJson));

        return CreateManyAsync(requests, cancellationToken);
    }

    public async Task<AuthResult<IReadOnlyList<NotificationDto>>> GetForUserAsync(
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var items = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto(
                n.NotificationId,
                n.Type.ToString(),
                n.Title,
                n.Body,
                n.Data,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        return AuthResult<IReadOnlyList<NotificationDto>>.Success(items);
    }

    public async Task<AuthResult<UnreadCountDto>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        return AuthResult<UnreadCountDto>.Success(new UnreadCountDto(count));
    }

    public async Task<AuthResult<bool>> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(
                n => n.NotificationId == notificationId && n.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return AuthResult<bool>.Failure(["Notification not found."]);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return AuthResult<bool>.Success(true);
    }

    public async Task<AuthResult<int>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return AuthResult<int>.Success(unread.Count);
    }

    private async Task PushAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            object? data = null;
            if (!string.IsNullOrWhiteSpace(request.Data))
            {
                try
                {
                    data = JsonSerializer.Deserialize<JsonElement>(request.Data);
                }
                catch (JsonException)
                {
                    data = new { raw = request.Data };
                }
            }

            await _webPushSender.SendToUserAsync(
                request.UserId,
                request.Title,
                request.Body,
                new
                {
                    type = request.Type.ToString(),
                    url = GetDeepLink(request.Type),
                    payload = data,
                },
                cancellationToken);
        }
        catch
        {
            // Push failures must never break in-app notification creation.
        }
    }

    private static string GetDeepLink(NotificationType type) =>
        type switch
        {
            NotificationType.ExercisesAssigned
                or NotificationType.ProgramCreated
                or NotificationType.ExerciseReminder => "/patient/exercises",
            NotificationType.PatientLinkRequested
                or NotificationType.ExercisesCompleted
                or NotificationType.DailyFeedbackReceived => "/doctor/patients",
            NotificationType.DoctorPendingActivation => "/admin/doctors",
            NotificationType.LinkApproved
                or NotificationType.LinkRejected
                or NotificationType.PatientRemoved => "/patient/doctors",
            NotificationType.DoctorActivated => "/doctor",
            _ => "/",
        };

    private static Notification ToEntity(CreateNotificationRequest request) =>
        new()
        {
            NotificationId = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Body = request.Body,
            Data = request.Data,
            IsRead = false,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static string? SerializeData(object? data) =>
        data is null ? null : JsonSerializer.Serialize(data, JsonOptions);
}

internal sealed class NullWebPushSender : IWebPushSender
{
    public static NullWebPushSender Instance { get; } = new();

    public string? PublicKey => null;

    public Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
