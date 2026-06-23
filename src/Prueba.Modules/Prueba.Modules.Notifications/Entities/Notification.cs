using Prueba.Domain.Entities;

namespace Prueba.Modules.Notifications.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime SentAt { get; private set; }

    private Notification() { } // EF Core

    public static Notification Create(
        Guid userId,
        string type,
        string title,
        string message,
        Guid tenantId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be empty.", nameof(type));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
