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

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
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

        var result = await _notificationService.MarkAllAsReadAsync(userId.Value, cancellationToken);
        return Ok(new UnreadCountDto(0));
    }
}
