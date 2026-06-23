using FluentAssertions;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Properties.Entities;

public class AvailabilityTests
{
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldCreateAvailability()
    {
        // Arrange
        var date = new DateOnly(2026, 7, 15);

        // Act
        var availability = Availability.Create(
            propertyId: _propertyId,
            date: date,
            isAvailable: true,
            price: 150.00m,
            tenantId: _tenantId);

        // Assert
        availability.Id.Should().NotBe(Guid.Empty);
        availability.PropertyId.Should().Be(_propertyId);
        availability.Date.Should().Be(date);
        availability.IsAvailable.Should().BeTrue();
        availability.Price.Should().Be(150.00m);
        availability.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public void Create_WithNegativePrice_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => Availability.Create(
            propertyId: _propertyId,
            date: DateOnly.FromDateTime(DateTime.Today),
            isAvailable: true,
            price: -10m,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkUnavailable_ShouldSetIsAvailableToFalse()
    {
        // Arrange
        var availability = Availability.Create(
            propertyId: _propertyId,
            date: DateOnly.FromDateTime(DateTime.Today),
            isAvailable: true,
            price: 100m,
            tenantId: _tenantId);

        // Act
        availability.MarkUnavailable();

        // Assert
        availability.IsAvailable.Should().BeFalse();
        availability.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAvailable_ShouldSetIsAvailableToTrue()
    {
        // Arrange
        var availability = Availability.Create(
            propertyId: _propertyId,
            date: DateOnly.FromDateTime(DateTime.Today),
            isAvailable: false,
            price: 100m,
            tenantId: _tenantId);

        // Act
        availability.MarkAvailable();

        // Assert
        availability.IsAvailable.Should().BeTrue();
        availability.UpdatedAt.Should().NotBeNull();
    }
}
