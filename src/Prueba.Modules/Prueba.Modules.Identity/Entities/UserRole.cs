using Prueba.Domain.Entities;

namespace Prueba.Modules.Identity.Entities;

public class UserRole : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;

    private UserRole() { } // EF Core

    public static UserRole Create(Guid userId, Guid roleId, Guid tenantId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (roleId == Guid.Empty)
            throw new ArgumentException("Role ID cannot be empty.", nameof(roleId));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        return new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
