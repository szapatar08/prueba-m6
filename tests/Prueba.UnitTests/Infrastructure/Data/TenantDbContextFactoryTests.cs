using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;

namespace Prueba.UnitTests.Infrastructure.Data;

public class TenantDbContextFactoryTests
{
    private readonly Mock<ICurrentTenant> _tenantMock;
    private readonly DbContextOptions<AppDbContext> _options;

    public TenantDbContextFactoryTests()
    {
        _tenantMock = new Mock<ICurrentTenant>();
        _tenantMock.Setup(t => t.SchemaName).Returns("tenant_test");
        _tenantMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public void Create_ShouldReturnAppDbContext()
    {
        // Arrange
        var factory = new TenantDbContextFactory(_options, _tenantMock.Object);

        // Act
        using var context = factory.Create();

        // Assert
        context.Should().NotBeNull();
        context.Should().BeOfType<AppDbContext>();
    }

    [Fact]
    public void Create_ShouldPassOptionsToContext()
    {
        // Arrange
        var factory = new TenantDbContextFactory(_options, _tenantMock.Object);

        // Act
        using var context = factory.Create();

        // Assert
        context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
    }

    [Fact]
    public void Create_MultipleCalls_ShouldReturnNewInstanceEachTime()
    {
        // Arrange
        var factory = new TenantDbContextFactory(_options, _tenantMock.Object);

        // Act
        using var context1 = factory.Create();
        using var context2 = factory.Create();

        // Assert
        context1.Should().NotBeSameAs(context2);
    }
}
