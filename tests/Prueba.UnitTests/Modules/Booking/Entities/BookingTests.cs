using FluentAssertions;
using Prueba.Modules.Booking.Entities;

namespace Prueba.UnitTests.Modules.Booking.Entities;

public class BookingTests
{
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldReturnBooking()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);
        var totalPrice = 600.00m;

        // Act
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, totalPrice, _tenantId);

        // Assert
        booking.Should().NotBeNull();
        booking.PropertyId.Should().Be(_propertyId);
        booking.GuestId.Should().Be(_guestId);
        booking.StartDate.Should().Be(startDate);
        booking.EndDate.Should().Be(endDate);
        booking.TotalPrice.Should().Be(totalPrice);
        booking.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public void Create_ShouldSetStatusToPending()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Create_ShouldSetCheckInTimeTo2PM()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        booking.CheckInTime.Should().Be(new TimeOnly(14, 0)); // 2:00 PM
    }

    [Fact]
    public void Create_ShouldSetCheckOutTimeTo12PM()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        booking.CheckOutTime.Should().Be(new TimeOnly(12, 0)); // 12:00 PM
    }

    [Fact]
    public void Create_ShouldSetCreatedAt()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        booking.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateNewId()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        booking.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithEmptyPropertyId_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var act = () => BookingEntity.Create(
            Guid.Empty, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Property ID*");
    }

    [Fact]
    public void Create_WithEmptyGuestId_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var act = () => BookingEntity.Create(
            _propertyId, Guid.Empty, startDate, endDate, 400m, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Guest ID*");
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 5);
        var endDate = new DateOnly(2026, 7, 1);

        // Act
        var act = () => BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*End date*");
    }

    [Fact]
    public void Create_WithSameStartAndEndDate_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 1);

        // Act
        var act = () => BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*End date*");
    }

    [Fact]
    public void Create_WithNegativeTotalPrice_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var act = () => BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, -100m, _tenantId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*total price*");
    }

    [Fact]
    public void Create_WithZeroTotalPrice_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var act = () => BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 0m, _tenantId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*total price*");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        var act = () => BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tenant ID*");
    }

    [Fact]
    public void Confirm_WhenPending_ShouldTransitionToConfirmed()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Act
        booking.Confirm();

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WhenNotPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Confirm(); // Now Confirmed

        // Act
        var act = () => booking.Confirm();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot confirm*");
    }

    [Fact]
    public void Cancel_WhenPending_ShouldTransitionToCancelled()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Act
        booking.Cancel();

        // Assert
        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenConfirmed_ShouldTransitionToCancelled()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Confirm();

        // Act
        booking.Cancel();

        // Assert
        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Cancel();

        // Act
        var act = () => booking.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    [Fact]
    public void Cancel_WhenCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 1, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Confirm();
        booking.Complete();

        // Act
        var act = () => booking.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    [Fact]
    public void Complete_WhenConfirmed_ShouldTransitionToCompleted()
    {
        // Arrange
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 1, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Confirm();

        // Act
        booking.Complete();

        // Assert
        booking.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public void Complete_WhenPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);

        // Act
        var act = () => booking.Complete();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot complete*");
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 1, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Confirm();
        booking.Complete();

        // Act
        var act = () => booking.Complete();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot complete*");
    }

    [Fact]
    public void Cancel_WhenCompleted_ShouldThrowInvalidOperationException_WithSpecificMessage()
    {
        // Arrange
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 1, 5);
        var booking = BookingEntity.Create(
            _propertyId, _guestId, startDate, endDate, 400m, _tenantId);
        booking.Confirm();
        booking.Complete();

        // Act
        var act = () => booking.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>();
        booking.Status.Should().Be(BookingStatus.Completed); // Status unchanged
    }
}
