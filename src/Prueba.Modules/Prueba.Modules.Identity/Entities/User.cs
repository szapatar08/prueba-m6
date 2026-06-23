using Prueba.Domain.Entities;

namespace Prueba.Modules.Identity.Entities;

public class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    private User() { } // EF Core

    public static User Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName?.Trim() ?? string.Empty,
            LastName = lastName?.Trim() ?? string.Empty,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
