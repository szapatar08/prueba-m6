using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Properties.Features.CreateProperty;

namespace Prueba.UnitTests.Modules.Properties.Features;

public class CreatePropertyHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly CreatePropertyHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public CreatePropertyHandlerTests()
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
        _handler = new CreatePropertyHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateProperty()
    {
        // Arrange
        var command = new CreatePropertyCommand(
            Name: "Beach House",
            Description: "Beautiful beach house",
            Location: "Beach front",
            Address: "123 Ocean Drive",
            City: "Miami",
            Country: "USA",
            PricePerNight: 150.00m,
            MaxGuests: 6,
            Bedrooms: 3,
            Bathrooms: 2);

        // Act
        var result = await _handler.Handle(command, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Beach House");
        result.Value.City.Should().Be("Miami");
        result.Value.Country.Should().Be("USA");
        result.Value.PricePerNight.Should().Be(150.00m);
        result.Value.OwnerId.Should().Be(_ownerId);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldPersistToDatabase()
    {
        // Arrange
        var command = new CreatePropertyCommand(
            Name: "Mountain Cabin",
            Description: "Cozy cabin",
            Location: "Mountains",
            Address: "456 Mountain Rd",
            City: "Denver",
            Country: "USA",
            PricePerNight: 200.00m,
            MaxGuests: 4,
            Bedrooms: 2,
            Bathrooms: 1);

        // Act
        var result = await _handler.Handle(command, _ownerId, CancellationToken.None);

        // Assert
        var saved = await _context.Set<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == result.Value!.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Mountain Cabin");
        saved.TenantId.Should().Be(_tenantId);
        saved.OwnerId.Should().Be(_ownerId);
    }

    [Fact]
    public async Task Handle_ShouldSetCorrectTenantId()
    {
        // Arrange
        var command = new CreatePropertyCommand(
            Name: "Test Property",
            Description: "Test",
            Location: "Test",
            Address: "Test",
            City: "TestCity",
            Country: "TestCountry",
            PricePerNight: 100m,
            MaxGuests: 2,
            Bedrooms: 1,
            Bathrooms: 1);

        // Act
        var result = await _handler.Handle(command, _ownerId, CancellationToken.None);

        // Assert
        var saved = await _context.Set<Property>()
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == result.Value!.Id);
        saved.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Handle_ShouldSetCreatedAt()
    {
        // Arrange
        var command = new CreatePropertyCommand(
            Name: "Time Test",
            Description: "Test",
            Location: "Test",
            Address: "Test",
            City: "TestCity",
            Country: "TestCountry",
            PricePerNight: 100m,
            MaxGuests: 2,
            Bedrooms: 1,
            Bathrooms: 1);

        // Act
        var result = await _handler.Handle(command, _ownerId, CancellationToken.None);

        // Assert
        result.Value!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
