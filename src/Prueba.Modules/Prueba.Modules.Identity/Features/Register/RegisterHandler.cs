using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Entities;

namespace Prueba.Modules.Identity.Features.Register;

public class RegisterHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public RegisterHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Check for duplicate email within the tenant
        // Use IgnoreQueryFilters for internal lookups — tenant scoping is enforced manually
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var existingUser = await _repository.Query<User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.TenantId == tenantId, cancellationToken);

        if (existingUser is not null)
            return Result<RegisterResponse>.Fail("Email is already registered");

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        // Create user
        var user = User.Create(
            email: command.Email,
            passwordHash: passwordHash,
            firstName: command.FirstName,
            lastName: command.LastName,
            tenantId: tenantId);

        _repository.Add(user);

        // Ensure "Guest" role exists and assign it
        var guestRole = await _repository.Query<Role>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == "Guest" && r.TenantId == tenantId, cancellationToken);

        if (guestRole is null)
        {
            guestRole = Role.Create("Guest", tenantId);
            _repository.Add(guestRole);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        var userRole = UserRole.Create(user.Id, guestRole.Id, tenantId);
        _repository.Add(userRole);

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<RegisterResponse>.Success(new RegisterResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.CreatedAt));
    }
}

public record RegisterResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime CreatedAt);
