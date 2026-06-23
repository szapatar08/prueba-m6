using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.CreateProperty;

public class CreatePropertyHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public CreatePropertyHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<PropertyResponse>> Handle(CreatePropertyCommand command, Guid ownerId, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var property = Property.Create(
            name: command.Name,
            description: command.Description,
            location: command.Location,
            address: command.Address,
            city: command.City,
            country: command.Country,
            pricePerNight: command.PricePerNight,
            maxGuests: command.MaxGuests,
            bedrooms: command.Bedrooms,
            bathrooms: command.Bathrooms,
            ownerId: ownerId,
            tenantId: tenantId);

        _repository.Add(property);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<PropertyResponse>.Success(new PropertyResponse(
            property.Id,
            property.Name,
            property.Description,
            property.Location,
            property.Address,
            property.City,
            property.Country,
            property.PricePerNight,
            property.MaxGuests,
            property.Bedrooms,
            property.Bathrooms,
            property.OwnerId,
            property.CreatedAt));
    }
}

public record PropertyResponse(
    Guid Id,
    string Name,
    string Description,
    string Location,
    string Address,
    string City,
    string Country,
    decimal PricePerNight,
    int MaxGuests,
    int Bedrooms,
    int Bathrooms,
    Guid OwnerId,
    DateTime CreatedAt);
