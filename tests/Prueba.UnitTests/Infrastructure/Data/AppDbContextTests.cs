using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Infrastructure.Data;

namespace Prueba.UnitTests.Infrastructure.Data;

public class AppDbContextTests
{
    private readonly Mock<ICurrentTenant> _tenantMock;

    public AppDbContextTests()
    {
        _tenantMock = new Mock<ICurrentTenant>();
        _tenantMock.Setup(t => t.SchemaName).Returns("tenant_test");
        _tenantMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
    }

    [Fact]
    public void Constructor_ShouldAcceptCurrentTenant()
    {
        // Arrange & Act
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options, _tenantMock.Object);

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public void Model_ShouldHaveGlobalQueryFilterOnTestEntity()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options, _tenantMock.Object);

        // Act
        var entityType = context.Model.FindEntityType(typeof(TestEntity));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetDeclaredQueryFilters().Should().NotBeEmpty();
    }

    [Fact]
    public void SaveChanges_ShouldSetCreatedAtOnNewEntities()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantMock.Setup(t => t.TenantId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options, _tenantMock.Object);
        var entity = new TestEntity { Name = "Test" };
        context.TestEntities.Add(entity);

        // Act
        context.SaveChanges();

        // Assert
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void SaveChanges_ShouldSetUpdatedAtOnModifiedEntities()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options, _tenantMock.Object);
        var entity = new TestEntity { Name = "Original" };
        context.TestEntities.Add(entity);
        context.SaveChanges();

        // Act
        entity.Name = "Modified";
        context.SaveChanges();

        // Assert
        entity.UpdatedAt.Should().NotBeNull();
        entity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
