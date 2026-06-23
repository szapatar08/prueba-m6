using FluentAssertions;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Handlers;

namespace Prueba.UnitTests.Modules.Notifications.Handlers;

public class BookingCreatedEventHandlerTests
{
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly BookingCreatedEventHandler _handler;

    public BookingCreatedEventHandlerTests()
    {
        _emailServiceMock = new Mock<IEmailService>();
        _handler = new BookingCreatedEventHandler(_emailServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldSendConfirmationEmail()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        await _handler.HandleAsync(bookingId, guestId, propertyId, startDate, endDate);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "guest@example.com",
            "Booking Created - Pending Confirmation",
            It.Is<string>(s =>
                s.Contains(bookingId.ToString()) &&
                s.Contains("2026-07-01") &&
                s.Contains("2026-07-05")),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailShouldContainCheckInOutTimes()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 7, 1);
        var endDate = new DateOnly(2026, 7, 5);

        // Act
        await _handler.HandleAsync(bookingId, guestId, propertyId, startDate, endDate);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("2:00 PM") && s.Contains("12:00 PM")),
            CancellationToken.None), Times.Once);
    }
}
