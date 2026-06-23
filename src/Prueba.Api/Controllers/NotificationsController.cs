using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public NotificationsController(
        IRepository repository,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Get the authenticated user's notifications
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var notifications = await _repository.Query<Notification>()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync(cancellationToken);

        return Ok(notifications);
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    [Authorize]
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var notification = await _repository.Query<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (notification is null)
            return NotFound();

        notification.MarkAsRead();
        await _repository.SaveChangesAsync(cancellationToken);

        return Ok(notification);
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [Authorize]
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var notifications = await _repository.Query<Notification>()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Ok(new { markedCount = notifications.Count });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in token");
        return Guid.Parse(userIdClaim);
    }
}
