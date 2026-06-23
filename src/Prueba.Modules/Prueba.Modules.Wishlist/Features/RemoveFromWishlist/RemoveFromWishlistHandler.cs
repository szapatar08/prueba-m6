using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.Modules.Wishlist.Features.RemoveFromWishlist;

public class RemoveFromWishlistHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public RemoveFromWishlistHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result> Handle(
        RemoveFromWishlistCommand command,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Use IgnoreQueryFilters + manual tenant filter for SQLite compatibility
        var item = await _repository.Query<WishlistItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w =>
                w.UserId == userId
                && w.PropertyId == command.PropertyId
                && w.TenantId == tenantId,
                cancellationToken);

        // Idempotent: removing non-existent item is not an error
        if (item is null)
            return Result.Success();

        _repository.Remove(item);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
