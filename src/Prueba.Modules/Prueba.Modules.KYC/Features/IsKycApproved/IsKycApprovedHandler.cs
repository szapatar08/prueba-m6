using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Features.IsKycApproved;

public class IsKycApprovedHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public IsKycApprovedHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<bool>> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var isApproved = await _repository.Query<KycValidation>()
            .IgnoreQueryFilters()
            .AnyAsync(
                k => k.UserId == userId
                     && k.TenantId == _currentTenant.TenantId!.Value
                     && k.Status == KycStatus.Approved,
                cancellationToken);

        return Result<bool>.Success(isApproved);
    }
}
