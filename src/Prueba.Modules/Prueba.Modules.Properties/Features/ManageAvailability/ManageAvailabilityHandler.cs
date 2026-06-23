using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.ManageAvailability;

public class ManageAvailabilityHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public ManageAvailabilityHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AvailabilityResponse>> SetAvailability(
        SetAvailabilityCommand command,
        Guid propertyId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var property = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<AvailabilityResponse>.Fail("Property not found");

        if (property.OwnerId != ownerId)
            return Result<AvailabilityResponse>.Fail("You can only manage availability for your own properties");

        if (command.Price <= 0)
            return Result<AvailabilityResponse>.Fail("Price must be positive");

        // Upsert: find existing or create new
        var existing = await _repository.Query<Availability>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.PropertyId == propertyId && a.Date == command.Date && a.TenantId == tenantId, cancellationToken);

        if (existing is not null)
        {
            if (command.IsAvailable)
                existing.MarkAvailable();
            else
                existing.MarkUnavailable();
        }
        else
        {
            var availability = Availability.Create(
                propertyId: propertyId,
                date: command.Date,
                isAvailable: command.IsAvailable,
                price: command.Price,
                tenantId: tenantId);

            _repository.Add(availability);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<AvailabilityResponse>.Success(new AvailabilityResponse(
            propertyId,
            command.Date,
            command.IsAvailable,
            command.Price));
    }

    public async Task<Result<List<AvailabilityResponse>>> SetAvailabilityRange(
        SetAvailabilityRangeCommand command,
        Guid propertyId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var property = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<List<AvailabilityResponse>>.Fail("Property not found");

        if (property.OwnerId != ownerId)
            return Result<List<AvailabilityResponse>>.Fail("You can only manage availability for your own properties");

        if (command.Price <= 0)
            return Result<List<AvailabilityResponse>>.Fail("Price must be positive");

        if (command.EndDate <= command.StartDate)
            return Result<List<AvailabilityResponse>>.Fail("End date must be after start date");

        var results = new List<AvailabilityResponse>();

        // Load existing availability entries for the range
        var existingEntries = await _repository.Query<Availability>()
            .IgnoreQueryFilters()
            .Where(a => a.PropertyId == propertyId
                && a.Date >= command.StartDate
                && a.Date < command.EndDate
                && a.TenantId == tenantId)
            .ToDictionaryAsync(a => a.Date, cancellationToken);

        for (var date = command.StartDate; date < command.EndDate; date = date.AddDays(1))
        {
            if (existingEntries.TryGetValue(date, out var existing))
            {
                if (command.IsAvailable)
                    existing.MarkAvailable();
                else
                    existing.MarkUnavailable();
            }
            else
            {
                var availability = Availability.Create(
                    propertyId: propertyId,
                    date: date,
                    isAvailable: command.IsAvailable,
                    price: command.Price,
                    tenantId: tenantId);

                _repository.Add(availability);
            }

            results.Add(new AvailabilityResponse(propertyId, date, command.IsAvailable, command.Price));
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<List<AvailabilityResponse>>.Success(results);
    }
}

public record AvailabilityResponse(
    Guid PropertyId,
    DateOnly Date,
    bool IsAvailable,
    decimal Price);
