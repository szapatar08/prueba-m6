using FluentAssertions;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.UnitTests.Modules.Wishlist.Entities;

public class WishlistItemTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldCreateWishlistItem()
    {
        // Act
        var item = WishlistItem.Create(_userId, _propertyId, _tenantId);

        // Assert
        item.Should().NotBeNull();
        item.Id.Should().NotBeEmpty();
        item.UserId.Should().Be(_userId);
        item.PropertyId.Should().Be(_propertyId);
        item.TenantId.Should().Be(_tenantId);
        item.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        // Act
        var item1 = WishlistItem.Create(_userId, _propertyId, _tenantId);
        var item2 = WishlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), _tenantId);

        // Assert
        item1.Id.Should().NotBe(item2.Id);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(Guid.Empty, _propertyId, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID*");
    }

    [Fact]
    public void Create_WithEmptyPropertyId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(_userId, Guid.Empty, _tenantId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Property ID*");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        // Act
        var act = () => WishlistItem.Create(_userId, _propertyId, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Tenant ID*");
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var item = WishlistItem.Create(_userId, _propertyId, _tenantId);

        // Assert
        var after = DateTime.UtcNow;
        item.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
