using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.BrowseCatalog;

public class BrowseCatalogHandler
{
    private readonly IRepository _repository;

    public BrowseCatalogHandler(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<BrowseCatalogResult>> Handle(BrowseCatalogQuery query, CancellationToken cancellationToken)
    {
        // Public catalog — IgnoreQueryFilters for public browsing, apply tenant filter if present
        var propertiesQuery = _repository.Query<Property>().IgnoreQueryFilters();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLowerInvariant();
            propertiesQuery = propertiesQuery.Where(p => p.City.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            var country = query.Country.Trim().ToLowerInvariant();
            propertiesQuery = propertiesQuery.Where(p => p.Country.ToLower() == country);
        }

        if (query.MinGuests.HasValue)
        {
            propertiesQuery = propertiesQuery.Where(p => p.MaxGuests >= query.MinGuests.Value);
        }

        // Date availability filter — property must have ALL requested dates available
        if (query.StartDate.HasValue && query.EndDate.HasValue)
        {
            var startDate = query.StartDate.Value;
            var endDate = query.EndDate.Value;

            // Generate the list of required dates
            var requiredDates = new List<DateOnly>();
            for (var date = startDate; date < endDate; date = date.AddDays(1))
            {
                requiredDates.Add(date);
            }

            // Property must have availability entries for ALL required dates that are available
            propertiesQuery = propertiesQuery.Where(p =>
                p.Availability.Count(a =>
                    requiredDates.Contains(a.Date) && a.IsAvailable) == requiredDates.Count);
        }

        // Get total count before pagination
        var totalCount = await propertiesQuery.CountAsync(cancellationToken);

        // Apply pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var properties = await propertiesQuery
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(pageSize)
            .Select(p => new CatalogPropertyResponse(
                p.Id,
                p.Name,
                p.Description,
                p.Location,
                p.City,
                p.Country,
                p.PricePerNight,
                p.MaxGuests,
                p.Bedrooms,
                p.Bathrooms,
                p.Images.FirstOrDefault(i => i.IsPrimary) != null
                    ? p.Images.First(i => i.IsPrimary).Url
                    : p.Images.FirstOrDefault() != null
                        ? p.Images.First().Url
                        : null))
            .ToListAsync(cancellationToken);

        return Result<BrowseCatalogResult>.Success(new BrowseCatalogResult(
            properties,
            totalCount,
            page,
            pageSize));
    }
}

public record CatalogPropertyResponse(
    Guid Id,
    string Name,
    string Description,
    string Location,
    string City,
    string Country,
    decimal PricePerNight,
    int MaxGuests,
    int Bedrooms,
    int Bathrooms,
    string? PrimaryImageUrl);

public record BrowseCatalogResult(
    List<CatalogPropertyResponse> Properties,
    int TotalCount,
    int Page,
    int PageSize);
