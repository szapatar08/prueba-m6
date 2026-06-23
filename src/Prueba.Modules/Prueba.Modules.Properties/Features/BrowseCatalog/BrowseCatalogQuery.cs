namespace Prueba.Modules.Properties.Features.BrowseCatalog;

public record BrowseCatalogQuery(
    string? City = null,
    string? Country = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int? MinGuests = null,
    int Page = 1,
    int PageSize = 20);
