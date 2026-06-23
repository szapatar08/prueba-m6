using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Features.DeleteProperty;

public class DeletePropertyHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public DeletePropertyHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result> Handle(Guid propertyId, Guid ownerId, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var property = await _repository.Query<Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result.Fail("Property not found");

        if (property.OwnerId != ownerId)
            return Result.Fail("You can only delete your own properties");

        _repository.Remove(property);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
