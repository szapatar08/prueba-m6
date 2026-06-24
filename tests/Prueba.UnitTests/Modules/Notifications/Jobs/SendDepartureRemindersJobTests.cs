using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Jobs;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Notifications.Jobs;

public class SendDepartureRemindersJobTests
{
    private readonly Mock<IRepository> _repositoryMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IEmailTemplateRenderer> _templateRendererMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<SendDepartureRemindersJob>> _loggerMock;
    private readonly SendDepartureRemindersJob _job;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SendDepartureRemindersJobTests()
    {
        _repositoryMock = new Mock<IRepository>();
        _currentTenantMock = new Mock<ICurrentTenant>();
        _templateRendererMock = new Mock<IEmailTemplateRenderer>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<SendDepartureRemindersJob>>();
        _currentTenantMock.Setup(t => t.TenantId).Returns(_tenantId);
        _job = new SendDepartureRemindersJob(
            _repositoryMock.Object,
            _currentTenantMock.Object,
            _templateRendererMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendReminderForBookingsWithCheckOutToday()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var booking = BookingEntity.Create(propertyId, guestId, today.AddDays(-3), today, 300m, _tenantId);
        booking.Confirm();

        var guest = User.Create("guest@test.com", "hash", "John", "Doe", _tenantId);
        var property = Property.Create("Beach House", "Nice place", "Beach", "123 Ocean Ave", "Miami", "USA", 100m, 4, 2, 1, Guid.NewGuid(), _tenantId);

        var bookings = new List<BookingEntity> { booking }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<BookingEntity>()).Returns(bookings);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);
        _repositoryMock.Setup(r => r.GetByIdAsync<Property>(propertyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(property);

        _templateRendererMock.Setup(r => r.Render(TemplateTypes.DepartureReminder, It.IsAny<object>()))
            .Returns(("Check-out Reminder", "<html>Departure body</html>"));

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "guest@test.com",
            "Check-out Reminder",
            "<html>Departure body</html>",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotSendEmails_WhenNoCheckoutsToday()
    {
        // Arrange — booking ends tomorrow, not today
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var booking = BookingEntity.Create(propertyId, guestId, tomorrow.AddDays(-3), tomorrow, 300m, _tenantId);
        booking.Confirm();

        var bookings = new List<BookingEntity> { booking }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<BookingEntity>()).Returns(bookings);

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOnlyProcessConfirmedBookings()
    {
        // Arrange — booking ending today but in Cancelled status
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var booking = BookingEntity.Create(propertyId, guestId, today.AddDays(-3), today, 300m, _tenantId);
        booking.Confirm();
        booking.Cancel();

        var bookings = new List<BookingEntity> { booking }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<BookingEntity>()).Returns(bookings);

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueProcessing_WhenGuestEmailResolutionFails()
    {
        // Arrange — two bookings, one with invalid guest
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var guestId1 = Guid.NewGuid();
        var guestId2 = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var booking1 = BookingEntity.Create(propertyId, guestId1, today.AddDays(-3), today, 300m, _tenantId);
        booking1.Confirm();
        var booking2 = BookingEntity.Create(propertyId, guestId2, today.AddDays(-2), today, 200m, _tenantId);
        booking2.Confirm();

        var bookings = new List<BookingEntity> { booking1, booking2 }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<BookingEntity>()).Returns(bookings);

        // First guest not found, second guest found
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var guest2 = User.Create("guest2@test.com", "hash", "Jane", "Doe", _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest2);

        var property = Property.Create("Beach House", "Nice place", "Beach", "123 Ocean Ave", "Miami", "USA", 100m, 4, 2, 1, Guid.NewGuid(), _tenantId);
        _repositoryMock.Setup(r => r.GetByIdAsync<Property>(propertyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(property);

        _templateRendererMock.Setup(r => r.Render(TemplateTypes.DepartureReminder, It.IsAny<object>()))
            .Returns(("Check-out Reminder", "<html>Departure body</html>"));

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — only second booking's email sent
        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "guest2@test.com",
            "Check-out Reminder",
            "<html>Departure body</html>",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectTemplateDataToRenderer()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var guestId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var booking = BookingEntity.Create(propertyId, guestId, today.AddDays(-3), today, 300m, _tenantId);
        booking.Confirm();

        var guest = User.Create("guest@test.com", "hash", "John", "Doe", _tenantId);
        var property = Property.Create("Beach House", "Nice place", "Beach", "123 Ocean Ave", "Miami", "USA", 100m, 4, 2, 1, Guid.NewGuid(), _tenantId);

        var bookings = new List<BookingEntity> { booking }.AsQueryable();
        _repositoryMock.Setup(r => r.Query<BookingEntity>()).Returns(bookings);
        _repositoryMock.Setup(r => r.GetByIdAsync<User>(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guest);
        _repositoryMock.Setup(r => r.GetByIdAsync<Property>(propertyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(property);

        object? capturedData = null;
        _templateRendererMock.Setup(r => r.Render(TemplateTypes.DepartureReminder, It.IsAny<object>()))
            .Callback<string, object>((_, data) => capturedData = data)
            .Returns(("Subject", "Body"));

        // Act
        await _job.ExecuteAsync(CancellationToken.None);

        // Assert — verify template data contains expected fields
        capturedData.Should().NotBeNull();
        var dataDict = capturedData!.GetType().GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(capturedData));
        dataDict["GuestName"].Should().Be("John Doe");
        dataDict["PropertyName"].Should().Be("Beach House");
        dataDict["CheckOutTime"].ToString().Should().NotBeNullOrEmpty();
        dataDict["EndDate"].Should().Be(today.ToString("yyyy-MM-dd"));
    }
}
