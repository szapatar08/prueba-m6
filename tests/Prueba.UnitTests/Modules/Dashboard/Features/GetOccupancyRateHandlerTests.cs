using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Dashboard.Features.GetOccupancyRate;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Dashboard.Features;

/// <summary>
/// Test subclass that overrides the raw SQL occupancy calculation with a LINQ-based version
/// for SQLite compatibility. Production uses raw SQL (PostgreSQL).
/// </summary>
public class TestableGetOccupancyRateHandler : GetOccupancyRateHandler
{
    private readonly IRepository _repository;

    public TestableGetOccupancyRateHandler(IRepository repository, ICurrentTenant currentTenant)
        : base(repository, currentTenant)
    {
        _repository = repository;
    }

    protected override async Task<int> CalculateBookedDaysAsync(
        Guid propertyId,
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var bookings = await _repository.Query<BookingEntity>()
            .IgnoreQueryFilters()
            .Where(b => b.PropertyId == propertyId
                && b.TenantId == tenantId
                && b.Status == BookingStatus.Confirmed
                && b.StartDate < endDate
                && b.EndDate > startDate)
            .ToListAsync(cancellationToken);

        // Calculate overlap days
        var totalDays = 0;
        foreach (var booking in bookings)
        {
            var overlapStart = booking.StartDate > startDate ? booking.StartDate : startDate;
            var overlapEnd = booking.EndDate < endDate ? booking.EndDate : endDate;
            totalDays += overlapEnd.DayNumber - overlapStart.DayNumber;
        }

        return totalDays;
    }
}

public class GetOccupancyRateHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly TestableGetOccupancyRateHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public GetOccupancyRateHandlerTests()
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
        _handler = new TestableGetOccupancyRateHandler(_repository, _currentTenantMock.Object);

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

        // Use reflection to set the Id
        typeof(BaseEntity).GetProperty("Id")!.SetValue(property, _propertyId);

        _repository.Add(property);
        _context.SaveChanges();
    }

    [Fact]
    public async Task Handle_WithNoBookings_ShouldReturnZeroOccupancy()
    {
        // Arrange
        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.OccupancyRate.Should().Be(0);
        result.Value.TotalDays.Should().Be(30);
        result.Value.BookedDays.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithConfirmedBooking_ShouldCalculateOccupancy()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BookedDays.Should().Be(5);
        result.Value.OccupancyRate.Should().BeApproximately(16.67m, 0.01m);
    }

    [Fact]
    public async Task Handle_WithPendingBooking_ShouldNotCountTowardsOccupancy()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        // Don't confirm - stays Pending
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BookedDays.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithCancelledBooking_ShouldNotCountTowardsOccupancy()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking.Confirm();
        booking.Cancel();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BookedDays.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithBookingPartiallyOutsidePeriod_ShouldCountOnlyOverlap()
    {
        // Arrange - booking from July 28 to Aug 5, period is Aug 1-31
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 7, 28), new DateOnly(2026, 8, 5), 500m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.BookedDays.Should().Be(4); // Aug 1-4 (4 days overlap)
    }

    [Fact]
    public async Task Handle_WithNonExistentProperty_ShouldReturnError()
    {
        // Arrange
        var query = new GetOccupancyRateQuery(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithPropertyOwnedByDifferentOwner_ShouldReturnError()
    {
        // Arrange
        var differentOwnerId = Guid.NewGuid();
        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, differentOwnerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithInvalidDateRange_ShouldReturnError()
    {
        // Arrange
        var query = new GetOccupancyRateQuery(
            _propertyId,
            new DateOnly(2026, 8, 31),
            new DateOnly(2026, 8, 1)); // End before start

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("End date must be after start date");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
