using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Dashboard.Features.GetRevenue;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Dashboard.Features;

/// <summary>
/// Test subclass that overrides the revenue calculation with a LINQ-based version
/// for SQLite compatibility. Production uses EF Core.
/// </summary>
public class TestableGetRevenueHandler : GetRevenueHandler
{
    private readonly IRepository _repository;

    public TestableGetRevenueHandler(IRepository repository, ICurrentTenant currentTenant)
        : base(repository, currentTenant)
    {
        _repository = repository;
    }

    protected override async Task<(decimal TotalRevenue, int BookingCount)> CalculateRevenueAsync(
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

        return (bookings.Sum(b => b.TotalPrice), bookings.Count);
    }
}

public class GetRevenueHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly TestableGetRevenueHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public GetRevenueHandlerTests()
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
        _handler = new TestableGetRevenueHandler(_repository, _currentTenantMock.Object);

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
    public async Task Handle_WithNoBookings_ShouldReturnZeroRevenue()
    {
        // Arrange
        var query = new GetRevenueQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRevenue.Should().Be(0);
        result.Value.BookingCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithConfirmedBookings_ShouldCalculateRevenue()
    {
        // Arrange
        var booking1 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 400m, _tenantId);
        booking1.Confirm();

        var booking2 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking2.Confirm();

        _repository.Add(booking1);
        _repository.Add(booking2);
        await _repository.SaveChangesAsync();

        var query = new GetRevenueQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRevenue.Should().Be(900m);
        result.Value.BookingCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithPendingBooking_ShouldNotCountTowardsRevenue()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        // Don't confirm - stays Pending
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetRevenueQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRevenue.Should().Be(0);
        result.Value.BookingCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithCancelledBooking_ShouldNotCountTowardsRevenue()
    {
        // Arrange
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15), 500m, _tenantId);
        booking.Confirm();
        booking.Cancel();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetRevenueQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRevenue.Should().Be(0);
        result.Value.BookingCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithBookingOutsidePeriod_ShouldNotCount()
    {
        // Arrange - booking in July, query for August
        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 500m, _tenantId);
        booking.Confirm();
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetRevenueQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRevenue.Should().Be(0);
        result.Value.BookingCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNonExistentProperty_ShouldReturnError()
    {
        // Arrange
        var query = new GetRevenueQuery(
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
        var query = new GetRevenueQuery(
            _propertyId,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31));

        // Act
        var result = await _handler.Handle(query, differentOwnerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
