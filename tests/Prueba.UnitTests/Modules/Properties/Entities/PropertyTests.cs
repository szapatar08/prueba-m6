using FluentAssertions;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Properties.Entities;

public class PropertyTests
{
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldCreateProperty()
    {
        // Act
        var property = Property.Create(
            name: "Beach House",
            description: "A beautiful beach house",
            location: "Beach front",
            address: "123 Ocean Drive",
            city: "Miami",
            country: "USA",
            pricePerNight: 150.00m,
            maxGuests: 6,
            bedrooms: 3,
            bathrooms: 2,
            ownerId: _ownerId,
            tenantId: _tenantId);

        // Assert
        property.Id.Should().NotBe(Guid.Empty);
        property.Name.Should().Be("Beach House");
        property.Description.Should().Be("A beautiful beach house");
        property.Location.Should().Be("Beach front");
        property.Address.Should().Be("123 Ocean Drive");
        property.City.Should().Be("Miami");
        property.Country.Should().Be("USA");
        property.PricePerNight.Should().Be(150.00m);
        property.MaxGuests.Should().Be(6);
        property.Bedrooms.Should().Be(3);
        property.Bathrooms.Should().Be(2);
        property.OwnerId.Should().Be(_ownerId);
        property.TenantId.Should().Be(_tenantId);
        property.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Property.Create(
            name: "",
            description: "desc",
            location: "loc",
            address: "addr",
            city: "Miami",
            country: "USA",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyOwnerId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => Property.Create(
            name: "Test",
            description: "desc",
            location: "loc",
            address: "addr",
            city: "Miami",
            country: "USA",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: Guid.Empty,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Owner ID*");
    }

    [Fact]
    public void Create_WithNegativePrice_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => Property.Create(
            name: "Test",
            description: "desc",
            location: "loc",
            address: "addr",
            city: "Miami",
            country: "USA",
            pricePerNight: -10m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithZeroMaxGuests_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => Property.Create(
            name: "Test",
            description: "desc",
            location: "loc",
            address: "addr",
            city: "Miami",
            country: "USA",
            pricePerNight: 100m,
            maxGuests: 0,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_WithValidParameters_ShouldUpdateProperty()
    {
        // Arrange
        var property = Property.Create(
            name: "Old Name",
            description: "Old desc",
            location: "Old loc",
            address: "Old addr",
            city: "OldCity",
            country: "OldCountry",
            pricePerNight: 100m,
            maxGuests: 2,
            bedrooms: 1,
            bathrooms: 1,
            ownerId: _ownerId,
            tenantId: _tenantId);

        // Act
        property.Update(
            name: "New Name",
            description: "New desc",
            location: "New loc",
            address: "New addr",
            city: "NewCity",
            country: "NewCountry",
            pricePerNight: 200m,
            maxGuests: 4,
            bedrooms: 2,
            bathrooms: 2);

        // Assert
        property.Name.Should().Be("New Name");
        property.Description.Should().Be("New desc");
        property.City.Should().Be("NewCity");
        property.Country.Should().Be("NewCountry");
        property.PricePerNight.Should().Be(200m);
        property.MaxGuests.Should().Be(4);
        property.Bedrooms.Should().Be(2);
        property.Bathrooms.Should().Be(2);
        property.UpdatedAt.Should().NotBeNull();
    }
}
