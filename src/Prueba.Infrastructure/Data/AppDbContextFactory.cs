using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=prueba;Username=postgres;Password=postgres");

        var tenant = new DesignTimeCurrentTenant();
        return new AppDbContext(optionsBuilder.Options, tenant);
    }
}

/// <summary>
/// Stub tenant for design-time operations (migrations).
/// Uses a fixed tenant ID so migrations generate consistently.
/// </summary>
internal class DesignTimeCurrentTenant : ICurrentTenant
{
    public Guid? TenantId => Guid.Empty;
    public string SchemaName => "public";
    public void SetTenant(Guid tenantId) { }
    public void SetSchema(string schemaName) { }
}
