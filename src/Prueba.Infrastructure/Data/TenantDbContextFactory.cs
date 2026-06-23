using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Data;

public class TenantDbContextFactory
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ICurrentTenant _currentTenant;

    public TenantDbContextFactory(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
    {
        _options = options;
        _currentTenant = currentTenant;
    }

    public AppDbContext Create()
    {
        return new AppDbContext(_options, _currentTenant);
    }
}
