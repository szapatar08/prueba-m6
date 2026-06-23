using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Wishlist.Entities;
using Prueba.Modules.Wishlist.Features.IsInWishlist;

namespace Prueba.UnitTests.Modules.Wishlist.Features;

public class IsInWishlistHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly IsInWishlistHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public IsInWishlistHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentTenantMock.Setup(x => x.SchemaName).Returns("public");

        _context = new AppDbContext(options, _currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);
        _handler = new IsInWishlistHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithItemInWishlist_ShouldReturnTrue()
    {
        // Arrange
        var item = WishlistItem.Create(_userId, _propertyId, _tenantId);
        _repository.Add(item);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(_propertyId, _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsInWishlist.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithItemNotInWishlist_ShouldReturnFalse()
    {
        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), _userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsInWishlist.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithDifferentUser_ShouldReturnFalse()
    {
        // Arrange — item belongs to another user
        var otherUserId = Guid.NewGuid();
        var item = WishlistItem.Create(otherUserId, _propertyId, _tenantId);
        _repository.Add(item);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(_propertyId, _userId);

        // Assert
        result.Value!.IsInWishlist.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithDifferentTenant_ShouldReturnFalse()
    {
        // Arrange — item belongs to another tenant
        var otherTenantId = Guid.NewGuid();
        var item = WishlistItem.Create(_userId, _propertyId, otherTenantId);
        _repository.Add(item);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync(_propertyId, _userId);

        // Assert
        result.Value!.IsInWishlist.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
