using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Wishlist.Entities;
using Prueba.Modules.Wishlist.Features.AddToWishlist;

namespace Prueba.UnitTests.Modules.Wishlist.Features;

public class AddToWishlistHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly AddToWishlistHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public AddToWishlistHandlerTests()
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
        _handler = new AddToWishlistHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidProperty_ShouldAddToWishlist()
    {
        // Arrange
        var command = new AddToWishlistCommand(_propertyId);

        // Act
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.PropertyId.Should().Be(_propertyId);
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var command = new AddToWishlistCommand(_propertyId);

        // Act
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert
        var saved = await _context.Set<WishlistItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == result.Value!.Id);
        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(_userId);
        saved.PropertyId.Should().Be(_propertyId);
        saved.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Handle_WithDuplicateProperty_ShouldReturnSuccess()
    {
        // Arrange — add once, then add again
        var command = new AddToWishlistCommand(_propertyId);
        await _handler.Handle(command, _userId, CancellationToken.None);

        // Act — second add (duplicate)
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert — idempotent, returns success
        result.IsSuccess.Should().BeTrue();
        result.Value!.PropertyId.Should().Be(_propertyId);
    }

    [Fact]
    public async Task Handle_WithDuplicate_ShouldNotCreateSecondEntry()
    {
        // Arrange
        var command = new AddToWishlistCommand(_propertyId);
        await _handler.Handle(command, _userId, CancellationToken.None);

        // Act
        await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert — only one entry should exist
        var count = await _context.Set<WishlistItem>()
            .IgnoreQueryFilters()
            .CountAsync(w => w.UserId == _userId && w.PropertyId == _propertyId && w.TenantId == _tenantId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentUsersCanSaveSameProperty()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var command = new AddToWishlistCommand(_propertyId);
        await _handler.Handle(command, _userId, CancellationToken.None);

        // Act
        var result = await _handler.Handle(command, otherUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var command = new AddToWishlistCommand(_propertyId);

        // Act
        var result = await _handler.Handle(command, _userId, CancellationToken.None);

        // Assert
        var after = DateTime.UtcNow;
        result.Value!.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
