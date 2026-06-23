namespace Prueba.Modules.Properties.Features.UpdateProperty;

public record UpdatePropertyCommand(
    string Name,
    string Description,
    string Location,
    string Address,
    string City,
    string Country,
    decimal PricePerNight,
    int MaxGuests,
    int Bedrooms,
    int Bathrooms);
