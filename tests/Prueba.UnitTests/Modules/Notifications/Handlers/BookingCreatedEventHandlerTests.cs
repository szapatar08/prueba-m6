using FluentAssertions;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Handlers;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Notifications.Handlers;

public class BookingCreatedEventHandlerTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IEmailTemplateRenderer> _templateRendererMock;
    private readonly BookingCreatedEventHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public BookingCreatedEventHandlerTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _emailServiceMock = new Mock<IEmailService>();
        _templateRendererMock = new Mock<IEmailTemplateRenderer>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _handler = new BookingCreatedEventHandler(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _emailServiceMock.Object,
            _templateRendererMock.Object);
    }

    private void SetupGuestBookingAndProperty(Guid guestId, Guid bookingId, Guid propertyId)
    {
        var guest = User.Create("real.guest@domain.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        var booking = BookingEntity.Create(propertyId, guestId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5), 500m, _tenantId);
        var property = Property.Create("Beach House", "Nice place", "Beach", "123 Ocean Dr", "Miami", "US", 100m, 4, 2, 1, Guid.NewGuid(), _tenantId);

        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);
        _repositoryMock.Setup(r => r.GetByIdAsync<BookingEntity>(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
        _repositoryMock.Setup(r => r.GetByIdAsync<Property>(propertyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(property);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseTemplateRendererForEmail()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        SetupGuestBookingAndProperty(guestId, bookingId, propertyId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.BookingCreated, It.IsAny<object>()))
            .Returns(("Booking Created - Beach House", "<html>created</html>"));

        // Act
        await _handler.HandleAsync(bookingId, guestId, propertyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5));

        // Assert
        _templateRendererMock.Verify(t => t.Render(TemplateTypes.BookingCreated, It.IsAny<object>()), Times.Once);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "real.guest@domain.com",
            "Booking Created - Beach House",
            "<html>created</html>",
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldResolveGuestEmailFromUserEntity()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        SetupGuestBookingAndProperty(guestId, bookingId, propertyId);
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.BookingCreated, It.IsAny<object>()))
            .Returns(("Subject", "Body"));

        // Act
        await _handler.HandleAsync(bookingId, guestId, propertyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5));

        // Assert — email sent to resolved user email, NOT hardcoded
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "real.guest@domain.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            CancellationToken.None), Times.Once);
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "guest@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldPassCorrectDataToRenderer()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        SetupGuestBookingAndProperty(guestId, bookingId, propertyId);
        object? capturedData = null;
        _templateRendererMock.Setup(t => t.Render(TemplateTypes.BookingCreated, It.IsAny<object>()))
            .Callback<string, object>((_, data) => capturedData = data)
            .Returns(("Subject", "Body"));

        // Act
        await _handler.HandleAsync(bookingId, guestId, propertyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5));

        // Assert
        capturedData.Should().NotBeNull();
        var json = System.Text.Json.JsonSerializer.Serialize(capturedData);
        json.Should().Contain("\"GuestName\":\"Guest User\"");
        json.Should().Contain("\"PropertyName\":\"Beach House\"");
        json.Should().Contain("\"StartDate\":\"2026-07-01\"");
        json.Should().Contain("\"EndDate\":\"2026-07-05\"");
        json.Should().Contain("\"CheckInTime\"");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenGuestNotFound()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(bookingId, guestId, propertyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Guest user*not found*");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenBookingNotFound()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guest = User.Create("real.guest@domain.com", BCrypt.Net.BCrypt.HashPassword("Password123!"),
            "Guest", "User", _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);
        _repositoryMock.Setup(r => r.GetByIdAsync<BookingEntity>(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookingEntity?)null);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(bookingId, guestId, propertyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Booking*not found*");
    }
}
