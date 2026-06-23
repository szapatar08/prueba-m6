using FluentAssertions;
using Prueba.Modules.Identity.Entities;

namespace Prueba.UnitTests.Modules.Identity.Entities;

public class RoleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        // Arrange & Act
        var role = Role.Create(name: "Admin", tenantId: _tenantId);

        // Assert
        role.Name.Should().Be("Admin");
        role.TenantId.Should().Be(_tenantId);
        role.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithEmptyName_ShouldThrowArgumentException(string name)
    {
        // Arrange & Act
        var act = () => Role.Create(name: name, tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var act = () => Role.Create(name: "Admin", tenantId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("tenantId");
    }
}
