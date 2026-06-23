using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Domain.Entities;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Dashboard.Features.GetBookingTrends;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Dashboard.Features;

public class GetBookingTrendsHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly GetBookingTrendsHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public GetBookingTrendsHandlerTests()
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
        _handler = new GetBookingTrendsHandler(_repository, _currentTenantMock.Object);

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
    public async Task Handle_WithNoBookings_ShouldReturnEmptyTrends()
    {
        // Arrange
        var query = new GetBookingTrendsQuery(
            _propertyId,
            TrendPeriod.Monthly);

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Trends.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithBookings_ShouldGroupByMonth()
    {
        // Arrange - bookings in different months (using past dates within last 12 months)
        var now = DateTime.UtcNow;
        var month1Start = new DateOnly(now.Year, now.Month, 1).AddMonths(-2);
        var month2Start = month1Start.AddMonths(1);

        var booking1 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), month1Start, month1Start.AddDays(4), 400m, _tenantId);
        booking1.Confirm();

        var booking2 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), month1Start.AddDays(9), month1Start.AddDays(14), 500m, _tenantId);
        booking2.Confirm();

        var booking3 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), month2Start, month2Start.AddDays(4), 300m, _tenantId);
        booking3.Confirm();

        _repository.Add(booking1);
        _repository.Add(booking2);
        _repository.Add(booking3);
        await _repository.SaveChangesAsync();

        var query = new GetBookingTrendsQuery(
            _propertyId,
            TrendPeriod.Monthly);

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Trends.Should().HaveCount(2);
        result.Value.Trends[0].BookingCount.Should().Be(2);
        result.Value.Trends[0].Revenue.Should().Be(900m);
        result.Value.Trends[1].BookingCount.Should().Be(1);
        result.Value.Trends[1].Revenue.Should().Be(300m);
    }

    [Fact]
    public async Task Handle_WithBookings_ShouldGroupByWeek()
    {
        // Arrange - bookings in different weeks (using current dates)
        var now = DateTime.UtcNow;
        var weekStart = DateOnly.FromDateTime(now.AddDays(-14));

        var booking1 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), weekStart, weekStart.AddDays(4), 400m, _tenantId);
        booking1.Confirm();

        var booking2 = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), weekStart.AddDays(7), weekStart.AddDays(11), 500m, _tenantId);
        booking2.Confirm();

        _repository.Add(booking1);
        _repository.Add(booking2);
        await _repository.SaveChangesAsync();

        var query = new GetBookingTrendsQuery(
            _propertyId,
            TrendPeriod.Weekly);

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Trends.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithPendingBookings_ShouldNotIncludeInTrends()
    {
        // Arrange - using current dates
        var now = DateTime.UtcNow;
        var startDate = DateOnly.FromDateTime(now.AddDays(-5));

        var booking = BookingEntity.Create(
            _propertyId, Guid.NewGuid(), startDate, startDate.AddDays(4), 400m, _tenantId);
        // Don't confirm - stays Pending
        _repository.Add(booking);
        await _repository.SaveChangesAsync();

        var query = new GetBookingTrendsQuery(
            _propertyId,
            TrendPeriod.Monthly);

        // Act
        var result = await _handler.Handle(query, _ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Trends.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentProperty_ShouldReturnError()
    {
        // Arrange
        var query = new GetBookingTrendsQuery(
            Guid.NewGuid(),
            TrendPeriod.Monthly);

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
        var query = new GetBookingTrendsQuery(
            _propertyId,
            TrendPeriod.Monthly);

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
