using FluentAssertions;
using Prueba.Modules.Notifications.Services;

namespace Prueba.UnitTests.Modules.Notifications.Services;

public class TemplateTypesTests
{
    [Fact]
    public void BookingCreated_ShouldHaveExpectedValue()
    {
        TemplateTypes.BookingCreated.Should().Be("BookingCreated");
    }

    [Fact]
    public void BookingConfirmed_ShouldHaveExpectedValue()
    {
        TemplateTypes.BookingConfirmed.Should().Be("BookingConfirmed");
    }

    [Fact]
    public void BookingCancelled_ShouldHaveExpectedValue()
    {
        TemplateTypes.BookingCancelled.Should().Be("BookingCancelled");
    }

    [Fact]
    public void KycApproved_ShouldHaveExpectedValue()
    {
        TemplateTypes.KycApproved.Should().Be("KycApproved");
    }

    [Fact]
    public void KycRejected_ShouldHaveExpectedValue()
    {
        TemplateTypes.KycRejected.Should().Be("KycRejected");
    }

    [Fact]
    public void ArrivalReminder_ShouldHaveExpectedValue()
    {
        TemplateTypes.ArrivalReminder.Should().Be("ArrivalReminder");
    }

    [Fact]
    public void DepartureReminder_ShouldHaveExpectedValue()
    {
        TemplateTypes.DepartureReminder.Should().Be("DepartureReminder");
    }

    [Fact]
    public void AllTypes_ShouldBeUnique()
    {
        // Arrange
        var types = new[]
        {
            TemplateTypes.BookingCreated,
            TemplateTypes.BookingConfirmed,
            TemplateTypes.BookingCancelled,
            TemplateTypes.KycApproved,
            TemplateTypes.KycRejected,
            TemplateTypes.ArrivalReminder,
            TemplateTypes.DepartureReminder
        };

        // Act & Assert
        types.Should().OnlyHaveUniqueItems();
    }
}
