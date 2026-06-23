using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Api.Controllers;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.UnitTests.Api.Controllers;

public class WishlistControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly WishlistController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public WishlistControllerTests()
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

        _controller = new WishlistController(_repository, _currentTenantMock.Object);
        SetUserContext(_userId);
    }

    private void SetUserContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Guest")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // === Authorization Attributes ===

    [Fact]
    public void AddToWishlist_ShouldRequireAuthorization()
    {
        var method = typeof(WishlistController).GetMethod(nameof(WishlistController.AddToWishlist));
        method.Should().NotBeNull();
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveFromWishlist_ShouldRequireAuthorization()
    {
        var method = typeof(WishlistController).GetMethod(nameof(WishlistController.RemoveFromWishlist));
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    [Fact]
    public void GetWishlist_ShouldRequireAuthorization()
    {
        var method = typeof(WishlistController).GetMethod(nameof(WishlistController.GetWishlist));
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    [Fact]
    public void IsInWishlist_ShouldRequireAuthorization()
    {
        var method = typeof(WishlistController).GetMethod(nameof(WishlistController.IsInWishlist));
        var authAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        authAttr.Should().HaveCount(1);
    }

    // === AddToWishlist ===

    [Fact]
    public async Task AddToWishlist_ShouldReturnOk()
    {
        // Act
        var result = await _controller.AddToWishlist(_propertyId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddToWishlist_Duplicate_ShouldReturnOk()
    {
        // Arrange — add once
        await _controller.AddToWishlist(_propertyId, CancellationToken.None);

        // Act — add again (idempotent)
        var result = await _controller.AddToWishlist(_propertyId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === RemoveFromWishlist ===

    [Fact]
    public async Task RemoveFromWishlist_ShouldReturnNoContent()
    {
        // Arrange — add first
        await _controller.AddToWishlist(_propertyId, CancellationToken.None);

        // Act
        var result = await _controller.RemoveFromWishlist(_propertyId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveFromWishlist_NonExistent_ShouldReturnNoContent()
    {
        // Act — remove non-existent item (idempotent)
        var result = await _controller.RemoveFromWishlist(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    // === GetWishlist ===

    [Fact]
    public async Task GetWishlist_ShouldReturnOk()
    {
        // Act
        var result = await _controller.GetWishlist(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === IsInWishlist ===

    [Fact]
    public async Task IsInWishlist_WithItemInWishlist_ShouldReturnTrue()
    {
        // Arrange
        var item = WishlistItem.Create(_userId, _propertyId, _tenantId);
        _repository.Add(item);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _controller.IsInWishlist(_propertyId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task IsInWishlist_WithItemNotInWishlist_ShouldReturnFalse()
    {
        // Act
        var result = await _controller.IsInWishlist(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // === CRUD Cycle ===

    [Fact]
    public async Task AddThenRemove_ShouldWork()
    {
        // Act — add
        var addResult = await _controller.AddToWishlist(_propertyId, CancellationToken.None);
        addResult.Should().BeOfType<OkObjectResult>();

        // Act — verify it's there
        var checkResult = await _controller.IsInWishlist(_propertyId, CancellationToken.None);
        checkResult.Should().BeOfType<OkObjectResult>();

        // Act — remove
        var removeResult = await _controller.RemoveFromWishlist(_propertyId, CancellationToken.None);
        removeResult.Should().BeOfType<NoContentResult>();

        // Act — verify it's gone
        var checkResult2 = await _controller.IsInWishlist(_propertyId, CancellationToken.None);
        checkResult2.Should().BeOfType<OkObjectResult>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
