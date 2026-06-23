using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Properties.Features.BrowseCatalog;

namespace Prueba.UnitTests.Modules.Properties.Features;

public class BrowseCatalogHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly BrowseCatalogHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public BrowseCatalogHandlerTests()
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
        _handler = new BrowseCatalogHandler(_repository);
    }

    private async Task SeedProperties()
    {
        var prop1 = Property.Create("Beach House", "Nice beach house", "Beach", "123 Beach Ave", "Miami", "USA", 150m, 6, 3, 2, _ownerId, _tenantId);
        var prop2 = Property.Create("Mountain Cabin", "Cozy cabin", "Mountains", "456 Mountain Rd", "Denver", "USA", 200m, 4, 2, 1, _ownerId, _tenantId);
        var prop3 = Property.Create("City Apartment", "Modern apartment", "Downtown", "789 Main St", "New York", "USA", 300m, 2, 1, 1, _ownerId, _tenantId);

        _context.Set<Property>().AddRange(prop1, prop2, prop3);
        await _context.SaveChangesAsync();

        // Add availability for prop1 (Miami) for a date range
        var startDate = new DateOnly(2026, 8, 1);
        for (var date = startDate; date < startDate.AddDays(5); date = date.AddDays(1))
        {
            var availability = Availability.Create(prop1.Id, date, true, 150m, _tenantId);
            _context.Set<Availability>().Add(availability);
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Handle_WithoutFilters_ShouldReturnAllProperties()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithCityFilter_ShouldReturnMatchingProperties()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery(City: "Miami");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(1);
        result.Value.Properties[0].City.Should().Be("Miami");
    }

    [Fact]
    public async Task Handle_WithCountryFilter_ShouldReturnMatchingProperties()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery(Country: "USA");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithMinGuestsFilter_ShouldReturnMatchingProperties()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery(MinGuests: 4);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(2); // Beach House (6) and Mountain Cabin (4)
    }

    [Fact]
    public async Task Handle_WithDateFilter_ShouldReturnOnlyAvailableProperties()
    {
        // Arrange
        await SeedProperties();
        // Beach House has availability Aug 1-5
        var query = new BrowseCatalogQuery(
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 3));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(1);
        result.Value.Properties[0].Name.Should().Be("Beach House");
    }

    [Fact]
    public async Task Handle_WithNoMatchingFilters_ShouldReturnEmpty()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery(City: "NonExistentCity");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery(Page: 1, PageSize: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CityFilterIsCaseInsensitive_ShouldMatch()
    {
        // Arrange
        await SeedProperties();
        var query = new BrowseCatalogQuery(City: "miami");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Properties.Should().HaveCount(1);
        result.Value.Properties[0].City.Should().Be("Miami");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
