using FluentAssertions;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Handlers;

namespace Prueba.UnitTests.Modules.Notifications.Handlers;

public class BookingCancelledEventHandlerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly BookingCancelledEventHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public BookingCancelledEventHandlerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _emailServiceMock = new Mock<IEmailService>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _handler = new BookingCancelledEventHandler(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _emailServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateNotification()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        // Act
        await _handler.HandleAsync(bookingId, guestId);

        // Assert
        _repositoryMock.Verify(r => r.Add(It.Is<Notification>(n =>
            n.UserId == guestId &&
            n.Type == "BookingCancelled" &&
            n.Title == "Booking Cancelled" &&
            n.TenantId == _tenantId)), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSendEmail()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();

        // Act
        await _handler.HandleAsync(bookingId, guestId);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "guest@example.com",
            "Booking Cancelled",
            It.Is<string>(s => s.Contains(bookingId.ToString())),
            CancellationToken.None), Times.Once);
    }
}
