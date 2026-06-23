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
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Properties.Features.CreateProperty;
using Prueba.Modules.Properties.Features.UpdateProperty;
using Prueba.Modules.Properties.Features.BrowseCatalog;

namespace Prueba.UnitTests.Api.Controllers;

public class PropertiesControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly PropertiesController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public PropertiesControllerTests()
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
        _controller = new PropertiesController(_repository, _currentTenantMock.Object);
    }

    private void SetUserContext(Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task Create_AsOwner_ShouldReturn201()
    {
        // Arrange
        SetUserContext(_ownerId, "Owner");
        var command = new CreatePropertyCommand(
            Name: "Test Property",
            Description: "A test property",
            Location: "Test Location",
            Address: "123 Test St",
            City: "Miami",
            Country: "USA",
            PricePerNight: 100m,
            MaxGuests: 4,
            Bedrooms: 2,
            Bathrooms: 1);

        // Act
        var result = await _controller.Create(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result;
        createdResult.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Create_AsGuest_ShouldReturn403()
    {
        // Arrange
        SetUserContext(Guid.NewGuid(), "Guest");
        var command = new CreatePropertyCommand(
            Name: "Test Property",
            Description: "A test property",
            Location: "Test Location",
            Address: "123 Test St",
            City: "Miami",
            Country: "USA",
            PricePerNight: 100m,
            MaxGuests: 4,
            Bedrooms: 2,
            Bathrooms: 1);

        // Act — the [Authorize(Roles = "Owner")] attribute would block this at the middleware level.
        // In unit tests without the full auth pipeline, we verify the attribute exists via reflection.
        var controllerType = typeof(PropertiesController);
        var createMethod = controllerType.GetMethod("Create")!;
        var authorizeAttr = createMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        authorizeAttr.Should().NotBeNull("Create endpoint must have [Authorize] attribute");
        authorizeAttr!.Roles.Should().Contain("Owner", "Create endpoint must require Owner role");
    }

    [Fact]
    public async Task GetById_WithExistingProperty_ShouldReturn200()
    {
        // Arrange
        var property = Property.Create(
            name: "Test Property",
            description: "Test",
            location: "Test",
            address: "Test",
            city: "Miami",
            country: "USA",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        _context.Set<Property>().Add(property);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _controller.GetById(property.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetById_WithNonExistentProperty_ShouldReturn404()
    {
        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Browse_WithoutFilters_ShouldReturnAllProperties()
    {
        // Arrange
        var property = Property.Create(
            name: "Test Property",
            description: "Test",
            location: "Test",
            address: "Test",
            city: "Miami",
            country: "USA",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        _context.Set<Property>().Add(property);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _controller.Browse(null, null, null, null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_AsOwnerOfProperty_ShouldReturn200()
    {
        // Arrange
        SetUserContext(_ownerId, "Owner");
        var property = Property.Create(
            name: "Old Name",
            description: "Old",
            location: "Old",
            address: "Old",
            city: "OldCity",
            country: "OldCountry",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        _context.Set<Property>().Add(property);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var command = new UpdatePropertyCommand(
            Name: "New Name",
            Description: "New desc",
            Location: "New loc",
            Address: "New addr",
            City: "NewCity",
            Country: "NewCountry",
            PricePerNight: 200m,
            MaxGuests: 4,
            Bedrooms: 2,
            Bathrooms: 2);

        // Act
        var result = await _controller.Update(property.Id, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_AsNonOwner_ShouldReturn403()
    {
        // Arrange
        var otherOwnerId = Guid.NewGuid();
        SetUserContext(otherOwnerId, "Owner");

        var property = Property.Create(
            name: "Test",
            description: "Test",
            location: "Test",
            address: "Test",
            city: "TestCity",
            country: "TestCountry",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId, // Owned by different user
            tenantId: _tenantId);

        _context.Set<Property>().Add(property);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var command = new UpdatePropertyCommand(
            Name: "Hacked Name",
            Description: "Hacked",
            Location: "Hacked",
            Address: "Hacked",
            City: "HackedCity",
            Country: "HackedCountry",
            PricePerNight: 1m,
            MaxGuests: 100,
            Bedrooms: 50,
            Bathrooms: 50);

        // Act
        var result = await _controller.Update(property.Id, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Delete_AsNonOwner_ShouldReturn403()
    {
        // Arrange
        var otherOwnerId = Guid.NewGuid();
        SetUserContext(otherOwnerId, "Owner");

        var property = Property.Create(
            name: "Test",
            description: "Test",
            location: "Test",
            address: "Test",
            city: "TestCity",
            country: "TestCountry",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        _context.Set<Property>().Add(property);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _controller.Delete(property.Id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Controller_ShouldHaveCorrectRoute()
    {
        // Assert
        var controllerType = typeof(PropertiesController);
        var routeAttr = controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>()
            .FirstOrDefault();

        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/[controller]");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
