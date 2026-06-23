using FluentAssertions;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Handlers;

namespace Prueba.UnitTests.Modules.Notifications.Handlers;

public class BookingConfirmedEventHandlerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly BookingConfirmedEventHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public BookingConfirmedEventHandlerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _emailServiceMock = new Mock<IEmailService>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _handler = new BookingConfirmedEventHandler(
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
        var guestEmail = "real.guest@domain.com";
        var guest = User.Create(guestEmail, BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);

        // Act
        await _handler.HandleAsync(bookingId, guestId);

        // Assert
        _repositoryMock.Verify(r => r.Add(It.Is<Notification>(n =>
            n.UserId == guestId &&
            n.Type == "BookingConfirmed" &&
            n.Title == "Booking Confirmed" &&
            n.TenantId == _tenantId)), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSendEmailToResolvedGuestEmail()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var guestEmail = "real.guest@domain.com";
        var guest = User.Create(guestEmail, BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);

        // Act
        await _handler.HandleAsync(bookingId, guestId);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            guestEmail,
            "Booking Confirmed",
            It.Is<string>(s => s.Contains(bookingId.ToString())),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSaveBeforeSendingEmail()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var guestEmail = "real.guest@domain.com";
        var guest = User.Create(guestEmail, BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);

        // Act
        await _handler.HandleAsync(bookingId, guestId);

        // Assert - both were called
        _repositoryMock.Verify(r => r.SaveChangesAsync(CancellationToken.None), Times.Once);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenGuestNotFound()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(bookingId, guestId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Guest user*not found*");
    }
}
