using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Prueba.IntegrationTests;

/// <summary>
/// Helper to generate JWT tokens for integration tests.
/// </summary>
public static class TestTokenGenerator
{
    private const string TestJwtKey = "TestSuperSecretKeyThatIsAtLeast32CharactersLong!";
    private const string TestIssuer = "PruebaApi";
    private const string TestAudience = "PruebaClient";

    public static string GenerateToken(Guid userId, string email, Guid tenantId, List<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateGuestToken(Guid userId, string email, Guid tenantId)
        => GenerateToken(userId, email, tenantId, new List<string> { "Guest" });

    public static string GenerateOwnerToken(Guid userId, string email, Guid tenantId)
        => GenerateToken(userId, email, tenantId, new List<string> { "Owner" });
}
