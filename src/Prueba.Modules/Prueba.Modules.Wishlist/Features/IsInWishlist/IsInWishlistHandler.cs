using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.Modules.Wishlist.Features.IsInWishlist;

public class IsInWishlistHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public IsInWishlistHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IsInWishlistResponse>> HandleAsync(
        Guid propertyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Use IgnoreQueryFilters + manual tenant filter for SQLite compatibility
        var exists = await _repository.Query<WishlistItem>()
            .IgnoreQueryFilters()
            .AnyAsync(w =>
                w.UserId == userId
                && w.PropertyId == propertyId
                && w.TenantId == tenantId,
                cancellationToken);

        return Result<IsInWishlistResponse>.Success(new IsInWishlistResponse(exists));
    }
}

public record IsInWishlistResponse(bool IsInWishlist);
