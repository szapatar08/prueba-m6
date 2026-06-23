using FluentAssertions;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.UnitTests.Modules.Notifications.Entities;

public class NotificationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldCreateNotification()
    {
        // Act
        var notification = Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);

        // Assert
        notification.Should().NotBeNull();
        notification.Id.Should().NotBeEmpty();
        notification.UserId.Should().Be(_userId);
        notification.Type.Should().Be("BookingConfirmed");
        notification.Title.Should().Be("Booking Confirmed");
        notification.Message.Should().Be("Your booking has been confirmed.");
        notification.IsRead.Should().BeFalse();
        notification.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        notification.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var notification1 = Notification.Create(_userId, "Type1", "Title1", "Message1", _tenantId);
        var notification2 = Notification.Create(Guid.NewGuid(), "Type2", "Title2", "Message2", _tenantId);

        // Assert
        notification1.Id.Should().NotBe(notification2.Id);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Notification.Create(
            Guid.Empty,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID*");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tenant ID*");
    }

    [Fact]
    public void Create_WithEmptyType_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Notification.Create(
            _userId,
            "",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type*");
    }

    [Fact]
    public void Create_WithEmptyTitle_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Notification.Create(
            _userId,
            "BookingConfirmed",
            "",
            "Your booking has been confirmed.",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title*");
    }

    [Fact]
    public void Create_WithEmptyMessage_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Message*");
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var notification = Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);

        // Assert
        var after = DateTime.UtcNow;
        notification.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_ShouldSetIsReadToFalse()
    {
        // Act
        var notification = Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);

        // Assert
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public void MarkAsRead_ShouldSetIsReadToTrue()
    {
        // Arrange
        var notification = Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);

        // Act
        notification.MarkAsRead();

        // Assert
        notification.IsRead.Should().BeTrue();
        notification.UpdatedAt.Should().NotBeNull();
        notification.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAsRead_OnAlreadyReadNotification_ShouldNotThrow()
    {
        // Arrange
        var notification = Notification.Create(
            _userId,
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking has been confirmed.",
            _tenantId);
        notification.MarkAsRead();

        // Act
        var act = () => notification.MarkAsRead();

        // Assert
        act.Should().NotThrow();
        notification.IsRead.Should().BeTrue();
    }
}
