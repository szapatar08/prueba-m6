using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.Modules.Wishlist.Features.AddToWishlist;

public class AddToWishlistHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public AddToWishlistHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<WishlistItemResponse>> Handle(
        AddToWishlistCommand command,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Check if already in wishlist — duplicate returns success (idempotent)
        // Use IgnoreQueryFilters + manual tenant filter for SQLite compatibility
        var existing = await _repository.Query<WishlistItem>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w =>
                w.UserId == userId
                && w.PropertyId == command.PropertyId
                && w.TenantId == tenantId,
                cancellationToken);

        if (existing is not null)
        {
            return Result<WishlistItemResponse>.Success(new WishlistItemResponse(
                existing.Id,
                existing.PropertyId,
                existing.CreatedAt));
        }

        var item = WishlistItem.Create(
            userId: userId,
            propertyId: command.PropertyId,
            tenantId: tenantId);

        _repository.Add(item);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<WishlistItemResponse>.Success(new WishlistItemResponse(
            item.Id,
            item.PropertyId,
            item.CreatedAt));
    }
}

public record WishlistItemResponse(
    Guid Id,
    Guid PropertyId,
    DateTime CreatedAt);
