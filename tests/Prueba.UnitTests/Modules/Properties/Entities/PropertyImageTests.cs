using FluentAssertions;
using Prueba.Modules.Properties.Entities;

namespace Prueba.UnitTests.Modules.Properties.Entities;

public class PropertyImageTests
{
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldCreateImage()
    {
        // Act
        var image = PropertyImage.Create(
            propertyId: _propertyId,
            url: "https://example.com/image.jpg",
            isPrimary: true,
            tenantId: _tenantId);

        // Assert
        image.Id.Should().NotBe(Guid.Empty);
        image.PropertyId.Should().Be(_propertyId);
        image.Url.Should().Be("https://example.com/image.jpg");
        image.IsPrimary.Should().BeTrue();
        image.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public void Create_WithEmptyUrl_ShouldThrowArgumentException()
    {
        // Act
        var act = () => PropertyImage.Create(
            propertyId: _propertyId,
            url: "",
            isPrimary: false,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyPropertyId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => PropertyImage.Create(
            propertyId: Guid.Empty,
            url: "https://example.com/image.jpg",
            isPrimary: false,
            tenantId: _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Property ID*");
    }
}
