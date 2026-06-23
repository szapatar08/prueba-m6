using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.Modules.Wishlist.Features.GetWishlist;

public class GetWishlistHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetWishlistHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<List<WishlistPropertyResponse>>> Handle(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Use IgnoreQueryFilters + manual tenant filter for SQLite compatibility
        var wishlistItems = await _repository.Query<WishlistItem>()
            .IgnoreQueryFilters()
            .Where(w => w.UserId == userId && w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fetch properties for the wishlist items — IgnoreQueryFilters for SQLite compatibility
        var propertyIds = wishlistItems.Select(w => w.PropertyId).ToList();

        var properties = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .Where(p => propertyIds.Contains(p.Id) && p.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var propertyMap = properties.ToDictionary(p => p.Id);

        var result = wishlistItems
            .Where(w => propertyMap.ContainsKey(w.PropertyId))
            .Select(w =>
            {
                var prop = propertyMap[w.PropertyId];
                return new WishlistPropertyResponse(
                    w.Id,
                    prop.Id,
                    prop.Name,
                    prop.City,
                    prop.Country,
                    prop.PricePerNight,
                    prop.MaxGuests,
                    w.CreatedAt);
            })
            .ToList();

        return Result<List<WishlistPropertyResponse>>.Success(result);
    }
}

public record WishlistPropertyResponse(
    Guid WishlistItemId,
    Guid PropertyId,
    string Name,
    string City,
    string Country,
    decimal PricePerNight,
    int MaxGuests,
    DateTime AddedAt);
