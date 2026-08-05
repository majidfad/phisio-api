using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Phisio.Api.Extensions;
using Phisio.Application.Notifications;

namespace Phisio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IPushSubscriptionService _pushSubscriptionService;
    private readonly IWebPushSender _webPushSender;

    public NotificationsController(
        INotificationService notificationService,
        IPushSubscriptionService pushSubscriptionService,
        IWebPushSender webPushSender)
    {
        _notificationService = notificationService;
        _pushSubscriptionService = pushSubscriptionService;
        _webPushSender = webPushSender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _notificationService.GetForUserAsync(userId.Value, take, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _notificationService.GetUnreadCountAsync(userId.Value, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("push/public-key")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VapidPublicKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPushPublicKey()
    {
        if (string.IsNullOrWhiteSpace(_webPushSender.PublicKey))
        {
            return NotFound(new { errors = new[] { "Web push is not configured." } });
        }

        return Ok(new VapidPublicKeyDto(_webPushSender.PublicKey));
    }

    [HttpPost("push/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubscribePush(
        [FromBody] PushSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
        {
            return BadRequest(new { errors = new[] { "Invalid push subscription." } });
        }

        await _pushSubscriptionService.UpsertAsync(
            userId.Value,
            request,
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("push/unsubscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnsubscribePush(
        [FromBody] PushSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            await _pushSubscriptionService.RemoveAsync(
                userId.Value,
                request.Endpoint,
                cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _notificationService.MarkAsReadAsync(
            userId.Value,
            notificationId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _notificationService.MarkAllAsReadAsync(userId.Value, cancellationToken);
        return Ok(new UnreadCountDto(0));
    }
}
