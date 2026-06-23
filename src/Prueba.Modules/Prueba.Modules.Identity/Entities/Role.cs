using Prueba.Domain.Entities;

namespace Prueba.Modules.Identity.Entities;

public class Role : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    private Role() { } // EF Core

    public static Role Create(string name, Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
