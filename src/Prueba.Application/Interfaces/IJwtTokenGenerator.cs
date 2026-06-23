using System.Security.Claims;

namespace Prueba.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string email, Guid tenantId, IEnumerable<string> roles);
    ClaimsPrincipal? ValidateToken(string token);
}
