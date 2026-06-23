using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.UpdateProperty;

public class UpdatePropertyHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public UpdatePropertyHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<UpdatePropertyResponse>> Handle(
        UpdatePropertyCommand command,
        Guid propertyId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var property = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<UpdatePropertyResponse>.Fail("Property not found");

        if (property.OwnerId != ownerId)
            return Result<UpdatePropertyResponse>.Fail("You can only update your own properties");

        property.Update(
            name: command.Name,
            description: command.Description,
            location: command.Location,
            address: command.Address,
            city: command.City,
            country: command.Country,
            pricePerNight: command.PricePerNight,
            maxGuests: command.MaxGuests,
            bedrooms: command.Bedrooms,
            bathrooms: command.Bathrooms);

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<UpdatePropertyResponse>.Success(new UpdatePropertyResponse(
            property.Id,
            property.Name,
            property.Description,
            property.City,
            property.Country,
            property.PricePerNight,
            property.MaxGuests,
            property.UpdatedAt!.Value));
    }
}

public record UpdatePropertyResponse(
    Guid Id,
    string Name,
    string Description,
    string City,
    string Country,
    decimal PricePerNight,
    int MaxGuests,
    DateTime UpdatedAt);
