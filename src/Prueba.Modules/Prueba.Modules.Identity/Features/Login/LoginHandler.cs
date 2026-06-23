using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Entities;

namespace Prueba.Modules.Identity.Features.Login;

public class LoginHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginHandler(IRepository repository, ICurrentTenant currentTenant, IJwtTokenGenerator jwtTokenGenerator)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Find user by email — use IgnoreQueryFilters and manually filter by tenant
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await _repository.Query<User>()
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.TenantId == tenantId, cancellationToken);

        if (user is null)
            return Result<LoginResponse>.Fail("Invalid email or password");

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash))
            return Result<LoginResponse>.Fail("Invalid email or password");

        // Get user roles
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        // Generate JWT token
        var token = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email,
            user.TenantId,
            roles);

        return Result<LoginResponse>.Success(new LoginResponse(
            token,
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            roles));
    }
}

public record LoginResponse(
    string Token,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    List<string> Roles);
