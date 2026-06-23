using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Booking.Events;
using Prueba.Modules.Booking.Features.ConfirmBooking;
using Prueba.Modules.Booking.Features.CancelBooking;
using Prueba.Modules.Booking.Features.CompleteBooking;

namespace Prueba.UnitTests.Modules.Booking.Features;

public class BookingStateTransitionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();

    public BookingStateTransitionTests()
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
    }

    private async Task<BookingEntity> CreatePendingBooking(DateOnly? start = null, DateOnly? end = null)
    {
        var startDate = start ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = end ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 500m, _tenantId);
        _repository.Add(booking);
        await _repository.SaveChangesAsync();
        return booking;
    }

    // === ConfirmBookingHandler Tests ===

    [Fact]
    public async Task Confirm_WithPendingBooking_ShouldSucceed()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        var handler = new ConfirmBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Confirm_WithNonExistentBooking_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new ConfirmBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Confirm_WithAlreadyConfirmedBooking_ShouldReturnError()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        booking.Confirm();
        await _repository.SaveChangesAsync();

        var handler = new ConfirmBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot confirm");
    }

    [Fact]
    public async Task Confirm_WithCancelledBooking_ShouldReturnError()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        booking.Cancel();
        await _repository.SaveChangesAsync();

        var handler = new ConfirmBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // === CancelBookingHandler Tests ===

    [Fact]
    public async Task Cancel_WithPendingBooking_ShouldSucceed()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        var handler = new CancelBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_WithConfirmedBookingBeforeCheckIn_ShouldSucceed()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var booking = await CreatePendingBooking(startDate, startDate.AddDays(5));
        booking.Confirm();
        await _repository.SaveChangesAsync();

        var handler = new CancelBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_WithNonExistentBooking_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new CancelBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(Guid.NewGuid(), _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Cancel_WhenNotGuest_ShouldReturnForbidden()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        var otherUserId = Guid.NewGuid();
        var handler = new CancelBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, otherUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("own bookings");
    }

    [Fact]
    public async Task Cancel_WithAlreadyCancelledBooking_ShouldReturnError()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        booking.Cancel();
        await _repository.SaveChangesAsync();

        var handler = new CancelBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot cancel");
    }

    [Fact]
    public async Task Cancel_WithCompletedBooking_ShouldReturnError()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        booking.Confirm();
        booking.Complete();
        await _repository.SaveChangesAsync();

        var handler = new CancelBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, _guestId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // === CompleteBookingHandler Tests ===

    [Fact]
    public async Task Complete_WithConfirmedBooking_ShouldSucceed()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        booking.Confirm();
        await _repository.SaveChangesAsync();

        var handler = new CompleteBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public async Task Complete_WithPendingBooking_ShouldReturnError()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        var handler = new CompleteBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot complete");
    }

    [Fact]
    public async Task Complete_WithNonExistentBooking_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new CompleteBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Complete_WithCancelledBooking_ShouldReturnError()
    {
        // Arrange
        var booking = await CreatePendingBooking();
        booking.Confirm();
        booking.Cancel();
        await _repository.SaveChangesAsync();

        var handler = new CompleteBookingHandler(_repository, _currentTenantMock.Object);

        // Act
        var result = await handler.Handle(booking.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // === Domain Events Tests ===

    [Fact]
    public void Confirm_ShouldRaiseBookingConfirmedEvent()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 500m, _tenantId);

        // Act
        booking.Confirm();

        // Assert
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmed);
    }

    [Fact]
    public void Cancel_ShouldRaiseBookingCancelledEvent()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 500m, _tenantId);

        // Act
        booking.Cancel();

        // Assert
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCancelled);
    }

    [Fact]
    public void BookingConfirmedEvent_ShouldContainBookingId()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 500m, _tenantId);

        // Act
        booking.Confirm();

        // Assert
        var evt = booking.DomainEvents.OfType<BookingConfirmed>().Single();
        evt.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void BookingCancelledEvent_ShouldContainBookingId()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 500m, _tenantId);

        // Act
        booking.Cancel();

        // Assert
        var evt = booking.DomainEvents.OfType<BookingCancelled>().Single();
        evt.BookingId.Should().Be(booking.Id);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
