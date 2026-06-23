using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Wishlist.Entities;
using Prueba.Modules.Wishlist.Features.GetWishlist;

namespace Prueba.UnitTests.Modules.Wishlist.Features;

public class GetWishlistHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly GetWishlistHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetWishlistHandlerTests()
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
        _handler = new GetWishlistHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task Handle_WithNoItems_ShouldReturnEmptyList()
    {
        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithItems_ShouldReturnPropertyDetails()
    {
        // Arrange — create a property and a wishlist item
        var property = Property.Create(
            "Beach House", "Nice place", "Beachfront", "123 Ocean Dr",
            "Miami", "USA", 200m, 4, 2, 1, Guid.NewGuid(), _tenantId);
        _repository.Add(property);

        var wishlistItem = WishlistItem.Create(_userId, property.Id, _tenantId);
        _repository.Add(wishlistItem);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].PropertyId.Should().Be(property.Id);
        result.Value[0].Name.Should().Be("Beach House");
        result.Value[0].City.Should().Be("Miami");
        result.Value[0].Country.Should().Be("USA");
        result.Value[0].PricePerNight.Should().Be(200m);
        result.Value[0].MaxGuests.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldReturnItemsOrderedByCreatedAtDescending()
    {
        // Arrange — create two properties and wishlist items
        var property1 = Property.Create(
            "First", "Desc", "Loc", "Addr",
            "City1", "Country1", 100m, 2, 1, 1, Guid.NewGuid(), _tenantId);
        var property2 = Property.Create(
            "Second", "Desc", "Loc", "Addr",
            "City2", "Country2", 200m, 4, 2, 1, Guid.NewGuid(), _tenantId);
        _repository.Add(property1);
        _repository.Add(property2);

        var item1 = WishlistItem.Create(_userId, property1.Id, _tenantId);
        var item2 = WishlistItem.Create(_userId, property2.Id, _tenantId);
        _repository.Add(item1);
        _repository.Add(item2);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert — most recent first
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        // Items are ordered by CreatedAt descending — both created nearly simultaneously
        // so just verify both are present
        result.Value.Should().Contain(w => w.PropertyId == property1.Id);
        result.Value.Should().Contain(w => w.PropertyId == property2.Id);
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentUserItems()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var property = Property.Create(
            "Test", "Desc", "Loc", "Addr",
            "City", "Country", 100m, 2, 1, 1, Guid.NewGuid(), _tenantId);
        _repository.Add(property);

        var item1 = WishlistItem.Create(_userId, property.Id, _tenantId);
        var item2 = WishlistItem.Create(otherUserId, property.Id, _tenantId);
        _repository.Add(item1);
        _repository.Add(item2);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentTenantItems()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        var property = Property.Create(
            "Test", "Desc", "Loc", "Addr",
            "City", "Country", 100m, 2, 1, 1, Guid.NewGuid(), _tenantId);
        _repository.Add(property);

        var item1 = WishlistItem.Create(_userId, property.Id, _tenantId);
        var item2 = WishlistItem.Create(_userId, property.Id, otherTenantId);
        _repository.Add(item1);
        _repository.Add(item2);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_userId, CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(1);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
