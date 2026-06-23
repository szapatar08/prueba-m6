using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Reports.Features.GenerateReport;

namespace Prueba.UnitTests.Modules.Reports.Features;

public class GenerateReportHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly GenerateReportHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _guestId = Guid.NewGuid();

    public GenerateReportHandlerTests()
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
        _handler = new GenerateReportHandler(_repository, _currentTenantMock.Object);

        // Seed a property
        var property = Property.Create(
            "Test Property",
            "A nice place",
            "Beach",
            "123 Main St",
            "Miami",
            "USA",
            100m,
            4,
            2,
            1,
            _ownerId,
            _tenantId);

        typeof(BaseEntity).GetProperty("Id")!.SetValue(property, _propertyId);

        _repository.Add(property);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Handle_WithNoBookings_ShouldReturnError()
    {
        // Arrange
        var query = new GenerateReportQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No bookings found");
    }

    [Fact]
    public async Task Handle_WithConfirmedBookings_ShouldGenerateExcelReport()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GenerateReportQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithPendingBookings_ShouldNotIncludeInReport()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        // Don't confirm - stays Pending
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GenerateReportQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No bookings found");
    }

    [Fact]
    public async Task Handle_WithPortfolioReport_ShouldIncludeAllProperties()
    {
        // Arrange - Create another property
        var property2 = Property.Create(
            "Test Property 2",
            "Another nice place",
            "Mountain",
            "456 Oak Ave",
            "Denver",
            "USA",
            200m,
            6,
            3,
            2,
            _ownerId,
            _tenantId);

        var property2Id = Guid.NewGuid();
        typeof(BaseEntity).GetProperty("Id")!.SetValue(property2, property2Id);

        _repository.Add(property2);

        // Add bookings to both properties
        var booking1 = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking1.Confirm();

        var booking2 = BookingEntity.Create(
            property2Id, Guid.NewGuid(), new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 25), 1000m, _tenantId);
        booking2.Confirm();

        _repository.Add(booking1);
        _repository.Add(booking2);
        await _repository.SaveChangesAsync();

        var query = new GenerateReportQuery(
            null, // null = portfolio report
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithBookingOutsidePeriod_ShouldNotInclude()
    {
        // Arrange - booking in July, query for August
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 500m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GenerateReportQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No bookings found");
    }

    [Fact]
    public async Task Handle_ShouldGenerateValidXlsxFile()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, _guestId, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GenerateReportQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Verify it's a valid XLSX file (starts with PK header)
        result.Value![0].Should().Be(0x50); // 'P'
        result.Value[1].Should().Be(0x4B); // 'K'
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
