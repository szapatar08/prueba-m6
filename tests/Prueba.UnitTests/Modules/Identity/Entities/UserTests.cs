using FluentAssertions;
using Prueba.Modules.Identity.Entities;

namespace Prueba.UnitTests.Modules.Identity.Entities;

public class UserTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        // Arrange & Act
        var user = User.Create(
            email: "test@example.com",
            passwordHash: "hashed_password",
            firstName: "John",
            lastName: "Doe",
            tenantId: _tenantId);

        // Assert
        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hashed_password");
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.TenantId.Should().Be(_tenantId);
        user.Id.Should().NotBeEmpty();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldInitializeUserRolesAsEmpty()
    {
        // Arrange & Act
        var user = User.Create(
            email: "test@example.com",
            passwordHash: "hashed_password",
            firstName: "John",
            lastName: "Doe",
            tenantId: _tenantId);

        // Assert
        user.UserRoles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithEmptyEmail_ShouldThrowArgumentException(string email)
    {
        // Arrange & Act
        var act = () => User.Create(
            email: email,
            passwordHash: "hashed_password",
            firstName: "John",
            lastName: "Doe",
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithEmptyPasswordHash_ShouldThrowArgumentException(string passwordHash)
    {
        // Arrange & Act
        var act = () => User.Create(
            email: "test@example.com",
            passwordHash: passwordHash,
            firstName: "John",
            lastName: "Doe",
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("passwordHash");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var act = () => User.Create(
            email: "test@example.com",
            passwordHash: "hashed_password",
            firstName: "John",
            lastName: "Doe",
            tenantId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("tenantId");
    }
}
