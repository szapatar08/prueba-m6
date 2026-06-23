using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.GetProperty;

public class GetPropertyHandler
{
    private readonly IRepository _repository;

    public GetPropertyHandler(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PropertyDetailResponse>> Handle(Guid propertyId, CancellationToken cancellationToken)
    {
        var property = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken);

        if (property is null)
            return Result<PropertyDetailResponse>.Fail("Property not found");

        var images = await _repository.Query<PropertyImage>()
            .IgnoreQueryFilters()
            .Where(i => i.PropertyId == propertyId)
            .OrderByDescending(i => i.IsPrimary)
            .Select(i => new ImageInfo(i.Id, i.Url, i.IsPrimary))
            .ToListAsync(cancellationToken);

        return Result<PropertyDetailResponse>.Success(new PropertyDetailResponse(
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
            property.CreatedAt,
            images));
    }
}

public record PropertyDetailResponse(
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
    DateTime CreatedAt,
    List<ImageInfo> Images);

public record ImageInfo(Guid Id, string Url, bool IsPrimary);
