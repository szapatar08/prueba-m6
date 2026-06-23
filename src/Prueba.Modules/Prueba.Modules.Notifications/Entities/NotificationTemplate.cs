using Prueba.Domain.Entities;

namespace Prueba.Modules.Notifications.Entities;

public class NotificationTemplate : BaseEntity
{
    public string Type { get; private set; } = string.Empty;
    public string SubjectTemplate { get; private set; } = string.Empty;
    public string BodyTemplate { get; private set; } = string.Empty;

    private NotificationTemplate() { } // EF Core

    public static NotificationTemplate Create(
        string type,
        string subjectTemplate,
        string bodyTemplate,
        Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be empty.", nameof(type));
        if (string.IsNullOrWhiteSpace(subjectTemplate))
            throw new ArgumentException("Subject template cannot be empty.", nameof(subjectTemplate));
        if (string.IsNullOrWhiteSpace(bodyTemplate))
            throw new ArgumentException("Body template cannot be empty.", nameof(bodyTemplate));

        return new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Type = type,
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
