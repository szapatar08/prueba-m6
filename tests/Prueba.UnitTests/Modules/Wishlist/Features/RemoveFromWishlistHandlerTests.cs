using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Wishlist.Entities;
using Prueba.Modules.Wishlist.Features.RemoveFromWishlist;

namespace Prueba.UnitTests.Modules.Wishlist.Features;

public class RemoveFromWishlistHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly RemoveFromWishlistHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public RemoveFromWishlistHandlerTests()
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
        _handler = new RemoveFromWishlistHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingItem_ShouldRemoveFromWishlist()
    {
        // Arrange
        var item = WishlistItem.Create(_userId, _propertyId, _tenantId);
        _repository.Add(item);
        await _repository.SaveChangesAsync();

        var command = new RemoveFromWishlistCommand(_propertyId);

        // Act
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var remaining = await _context.Set<WishlistItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == item.Id);
        remaining.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNonExistentItem_ShouldReturnSuccess()
    {
        // Arrange — nothing in wishlist
        var command = new RemoveFromWishlistCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert — idempotent, not an error
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldOnlyRemoveForCurrentUser()
    {
        // Arrange — two users have the same property in wishlist
        var otherUserId = Guid.NewGuid();
        var item1 = WishlistItem.Create(_userId, _propertyId, _tenantId);
        var item2 = WishlistItem.Create(otherUserId, _propertyId, _tenantId);
        _repository.Add(item1);
        _repository.Add(item2);
        await _repository.SaveChangesAsync();

        var command = new RemoveFromWishlistCommand(_propertyId);

        // Act — remove for first user only
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var otherUserItem = await _context.Set<WishlistItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.UserId == otherUserId && w.PropertyId == _propertyId);
        otherUserItem.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldOnlyRemoveForCurrentTenant()
    {
        // Arrange — same user+property but different tenant
        var otherTenantId = Guid.NewGuid();
        var item1 = WishlistItem.Create(_userId, _propertyId, _tenantId);
        var item2 = WishlistItem.Create(_userId, _propertyId, otherTenantId);
        _repository.Add(item1);
        _repository.Add(item2);
        await _repository.SaveChangesAsync();

        var command = new RemoveFromWishlistCommand(_propertyId);

        // Act
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert — only current tenant's item removed
        result.IsSuccess.Should().BeTrue();
        var otherTenantItem = await _context.Set<WishlistItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.UserId == _userId && w.PropertyId == _propertyId && w.TenantId == otherTenantId);
        otherTenantItem.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_RemovingTwice_ShouldBeIdempotent()
    {
        // Arrange
        var item = WishlistItem.Create(_userId, _propertyId, _tenantId);
        _repository.Add(item);
        await _repository.SaveChangesAsync();

        var command = new RemoveFromWishlistCommand(_propertyId);

        // Act
        await _handler.Handle(command, _userId, CancellationToken.None);
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert — second remove is not an error
        result.IsSuccess.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
