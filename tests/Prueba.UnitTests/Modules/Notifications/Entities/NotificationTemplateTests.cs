using FluentAssertions;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.UnitTests.Modules.Notifications.Entities;

public class NotificationTemplateTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldCreateTemplate()
    {
        // Act
        var template = NotificationTemplate.Create(
            "BookingConfirmed",
            "Booking Confirmed - {{PropertyId}}",
            "<h1>Booking Confirmed</h1><p>Your booking has been confirmed.</p>",
            _tenantId);

        // Assert
        template.Should().NotBeNull();
        template.Id.Should().NotBeEmpty();
        template.Type.Should().Be("BookingConfirmed");
        template.SubjectTemplate.Should().Be("Booking Confirmed - {{PropertyId}}");
        template.BodyTemplate.Should().Contain("<h1>Booking Confirmed</h1>");
        template.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var template1 = NotificationTemplate.Create("Type1", "Subject1", "Body1", _tenantId);
        var template2 = NotificationTemplate.Create("Type2", "Subject2", "Body2", _tenantId);

        // Assert
        template1.Id.Should().NotBe(template2.Id);
    }

    [Fact]
    public void Create_WithEmptyType_ShouldThrowArgumentException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            "",
            "Subject",
            "Body",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Type*");
    }

    [Fact]
    public void Create_WithEmptySubjectTemplate_ShouldThrowArgumentException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            "BookingConfirmed",
            "",
            "Body",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Subject template*");
    }

    [Fact]
    public void Create_WithEmptyBodyTemplate_ShouldThrowArgumentException()
    {
        // Act
        var act = () => NotificationTemplate.Create(
            "BookingConfirmed",
            "Subject",
            "",
            _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Body template*");
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var template = NotificationTemplate.Create(
            "BookingConfirmed",
            "Subject",
            "Body",
            _tenantId);

        // Assert
        var after = DateTime.UtcNow;
        template.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
