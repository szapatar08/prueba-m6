using Prueba.Domain.Entities;

namespace Prueba.Modules.Properties.Entities;

public class Property : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public decimal PricePerNight { get; private set; }
    public int MaxGuests { get; private set; }
    public int Bedrooms { get; private set; }
    public int Bathrooms { get; private set; }
    public Guid OwnerId { get; private set; }

    private readonly List<PropertyImage> _images = [];
    public IReadOnlyCollection<PropertyImage> Images => _images.AsReadOnly();

    private readonly List<Availability> _availability = [];
    public IReadOnlyCollection<Availability> Availability => _availability.AsReadOnly();

    private Property() { } // EF Core

    public static Property Create(
        string name,
        string description,
        string location,
        string address,
        string city,
        string country,
        decimal pricePerNight,
        int maxGuests,
        int bedrooms,
        int bathrooms,
        Guid ownerId,
        Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        if (ownerId == Guid.Empty)
            throw new ArgumentException("Owner ID cannot be empty.", nameof(ownerId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (pricePerNight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pricePerNight), "Price per night must be positive.");
        if (maxGuests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxGuests), "Max guests must be positive.");

        return new Property
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Location = location?.Trim() ?? string.Empty,
            Address = address?.Trim() ?? string.Empty,
            City = city.Trim(),
            Country = country.Trim(),
            PricePerNight = pricePerNight,
            MaxGuests = maxGuests,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms,
            OwnerId = ownerId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string description,
        string location,
        string address,
        string city,
        string country,
        decimal pricePerNight,
        int maxGuests,
        int bedrooms,
        int bathrooms)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        if (pricePerNight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pricePerNight), "Price per night must be positive.");
        if (maxGuests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxGuests), "Max guests must be positive.");

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Location = location?.Trim() ?? string.Empty;
        Address = address?.Trim() ?? string.Empty;
        City = city.Trim();
        Country = country.Trim();
        PricePerNight = pricePerNight;
        MaxGuests = maxGuests;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        UpdatedAt = DateTime.UtcNow;
    }
}
